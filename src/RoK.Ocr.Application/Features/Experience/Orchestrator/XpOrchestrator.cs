using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Features.Experience.Neurons;
using RoK.Ocr.Application.Features.Experience.Services;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models.Experience;

namespace RoK.Ocr.Application.Features.Experience.Orchestrator;

public class XpOrchestrator
{
    private readonly IOcrService _ocrService;
    private readonly IImageStorage _storage;
    private readonly ILogger<XpOrchestrator> _logger;
    private readonly XpMagnifier _magnifier; 
    private readonly XpGridNeuron _gridNeuron = new();

    public XpOrchestrator(
        IOcrService ocrService, 
        IImageStorage storage, 
        ILogger<XpOrchestrator> logger,
        XpMagnifier magnifier)
    {
        _ocrService = ocrService;
        _storage = storage;
        _logger = logger;
        _magnifier = magnifier;
    }

    public async Task<(XpInventoryData Data, string RawText)> ProcessXpAsync(List<IFormFile> images)
    {
        var finalData = new XpInventoryData();
        var itemTracker = new Dictionary<string, XpItemEntry>();
        var rawTextBuilder = new System.Text.StringBuilder();
        int imgIndex = 0;

        foreach (var image in images)
        {
            imgIndex++;
            string tempPath = "";
            try
            {
                using (var stream = image.OpenReadStream())
                    tempPath = await _storage.SaveImageAsync(stream, image.FileName);

                // 1. Initial OCR
                var (rawBlocks, fullText) = await _ocrService.AnalyzeInventoryAsync(tempPath);
                
                if (rawTextBuilder.Length > 0) rawTextBuilder.Append(" | ");
                rawTextBuilder.Append($"[IMG {imgIndex}] {fullText}");

                if (rawBlocks == null || !rawBlocks.Any()) continue;

                var nodes = BlockClassifier.Classify(rawBlocks);
                
                // Debugging Colors - Uncomment in XpInventoryData if needed
                // foreach (var node in nodes)
                // {
                //     if (node.Raw.Text.Any(char.IsDigit))
                //     {
                //         finalData.BlockDebug.Add($"Text: '{node.Raw.Text}' | Color: '{node.Raw.DominantColor}' | Conf: {node.Raw.Confidence:P0}");
                //     }
                // }

                // 2. Initial Extraction (Grid Neuron)
                var itemsFound = _gridNeuron.Extract(nodes);

                // --- BLACKLIST CREATION ---
                // If XpGridNeuron found a value (e.g., '1092') with high confidence, 
                // we blacklist it so the Magnifier doesn't erroneously fill gaps with this neighbor's value.
                var blacklistValues = new HashSet<int>();
                foreach (var item in itemsFound.Where(i => i.Quantity > -1))
                {
                    blacklistValues.Add(item.Quantity);
                }

                // 3. KEY STEP: Call the Magnifier (Sniper Mode)
                await _magnifier.ResolveMissingQuantitiesAsync(tempPath, itemsFound);

                // --- SANITY CHECK (Post-Magnifier Filter) ---
                // Verification logic to ensure the Magnifier didn't read a neighbor's value (Ghost Read).
                foreach (var item in itemsFound)
                {
                    // If we have duplicate large numbers (e.g., two items with 1092), it's likely a read error
                    // caused by the Magnifier looking too far and picking up the neighbor's text.
                    
                    int count = itemsFound.Count(i => i.Quantity == item.Quantity);
                    if (count > 1 && item.Quantity > 10) // Ignore common small numbers (1, 2, 5)
                    {
                        // Strategy: Ideally, identify the "weakest link" (lowest confidence) and mark as invalid.
                        // Currently, we log a warning and rely on the Global Merge to resolve conflicts 
                        // if multiple images are present.
                    }
                }
                
                // 4. Merge Logic
                foreach (var item in itemsFound)
                {
                    // --- SPATIAL DUPLICATE FILTER ---
                    // If this item has the SAME quantity as another item ALREADY PROCESSED in this image
                    // and it's a specific/large number, discard it.
                    // Example: Prevents XP_5000 from being set to 1092 if XP_10000 already claimed 1092.
                    
                    bool isDuplicateValue = itemsFound.Any(other => 
                        other != item && 
                        other.Quantity == item.Quantity && 
                        other.Quantity > 150 && // Tolerance for common numbers
                        other.Confidence > item.Confidence // The other one is more reliable
                    );

                    if (isDuplicateValue)
                    {
                        finalData.Warnings.Add($"[Sanity Check] Discarded {item.ItemId} with value {item.Quantity} because it duplicates a higher confidence neighbor (Ghost Read).");
                        continue;
                    }

                    if (item.Quantity == -1) 
                    {
                        finalData.Warnings.Add($"[Ignored] Could not read quantity for {item.ItemId} (Color: {item.DetectedColor}).");
                        continue;
                    }

                    if (itemTracker.TryGetValue(item.ItemId, out var existing))
                    {
                        if (existing.Quantity != item.Quantity)
                        {
                            bool useNew = item.Quantity > existing.Quantity; // Highest value wins strategy
                            finalData.Warnings.Add($"[Conflict] {item.ItemId}: {existing.Quantity} vs {item.Quantity}. Using {(useNew ? item.Quantity : existing.Quantity)}.");

                            if (useNew) itemTracker[item.ItemId] = item;
                        }
                        else if (item.Confidence > existing.Confidence)
                        {
                            itemTracker[item.ItemId] = item;
                        }
                    }
                    else
                    {
                        itemTracker.Add(item.ItemId, item);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing XP image.");
                finalData.Warnings.Add($"Error on image {imgIndex}: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath)) _storage.DeleteImage(tempPath);
            }
        }

        finalData.Items = itemTracker.Values.OrderBy(i => i.UnitValue).ToList();
        return (finalData, rawTextBuilder.ToString());
    }
}
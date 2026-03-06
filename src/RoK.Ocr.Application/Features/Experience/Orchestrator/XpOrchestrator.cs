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
using RoK.Ocr.Application.Common.Models; 
using RoK.Ocr.Domain.Models; 

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

    public async Task<(XpInventoryData Data, OcrAnalysisContext Context)> ProcessXpAsync(List<IFormFile> images, bool debugMode = false)
    {
        var finalData = new XpInventoryData();
        var itemTracker = new Dictionary<string, XpItemEntry>();
        var context = new OcrAnalysisContext(); 
        context.StartTimer("TotalOrchestration");

        context.Log("XpOrchestrator", $"Starting XP inventory analysis for {images.Count} images.");

        int imgIndex = 0;

        foreach (var image in images)
        {
            imgIndex++;
            context.StartTimer($"Image_{imgIndex}_Total"); 

            string tempPath = "";
            try
            {
                // 1. Save and OCR
                context.StartTimer($"Image_{imgIndex}_Storage");
                using (var stream = image.OpenReadStream())
                    tempPath = await _storage.SaveImageAsync(stream, image.FileName);
                context.StopTimer($"Image_{imgIndex}_Storage");

                context.StartTimer($"Image_{imgIndex}_Python");
                var (rawBlocks, fullText) = await _ocrService.AnalyzeInventoryAsync(tempPath);
                context.StopTimer($"Image_{imgIndex}_Python");

                // Get Image Dimensions for Context (using first image as reference)
                if (imgIndex == 1)
                {
                    using (var imgInfo = await SixLabors.ImageSharp.Image.LoadAsync(tempPath))
                    {
                        context.ImageWidth = imgInfo.Width;
                        context.ImageHeight = imgInfo.Height;
                        context.DebugInfo.ImagePath = tempPath;
                    }
                }

                context.Log("XpOrchestrator", $"[IMG {imgIndex}] OCR Scan Complete. Found {rawBlocks?.Count ?? 0} blocks.");
                
                // Log RawText in Debug
                if (debugMode && fullText.Length > 0)
                {
                    context.DebugInfo.RawText += $"--- IMG {imgIndex} ---\n{fullText}\n";
                }
                
                if (rawBlocks == null || !rawBlocks.Any()) continue;

                // 2. Initial Extraction
                context.StartTimer($"Image_{imgIndex}_Logic");
                var nodes = BlockClassifier.Classify(rawBlocks);
                var itemsFound = _gridNeuron.Extract(nodes);
                context.Log("XpOrchestrator", $"[IMG {imgIndex}] Initial extraction found {itemsFound.Count} potential items.");

                // 3. KEY STEP: Call the Magnifier (Sniper Mode)
                await _magnifier.ResolveMissingQuantitiesAsync(tempPath, itemsFound, context); 

                // --- SANITY CHECK & Merge Logic ---
                int recoveredCount = 0;
                
                foreach (var item in itemsFound)
                {
                    string fieldKey = $"xp_{item.ItemId}";

                    // Spatial Duplicate Filter
                    bool isDuplicateValue = itemsFound.Any(other =>
                        other != item && other.Quantity == item.Quantity && other.Quantity > 150 && other.Confidence > item.Confidence
                    );

                    if (isDuplicateValue)
                    {
                        context.LogWarning("ConsistencyAuditor", "WARN_GHOST_READ", $"Discarded {item.ItemId} (Qty: {item.Quantity}) because it duplicates a higher confidence neighbor (Ghost Read).", "LOW", fieldKey);
                        continue;
                    }

                    if (item.Quantity == -1)
                    {
                        context.LogWarning("ConsistencyAuditor", "WARN_QUANTITY_MISSING", $"Could not read quantity for {item.ItemId} (Color: {item.DetectedColor}).", "MEDIUM", fieldKey);
                        continue;
                    }
                    
                    if (item.Confidence > 0)
                    {
                        recoveredCount++; 

                        if (itemTracker.TryGetValue(item.ItemId, out var existing))
                        {
                            if (existing.Quantity != item.Quantity)
                            {
                                bool useNew = item.Quantity > existing.Quantity; 
                                context.LogWarning("ConsistencyAuditor", "WARN_ITEM_CONFLICT", $"Item '{item.ItemId}' conflict. Values: {existing.Quantity} vs {item.Quantity}. Using {(useNew ? item.Quantity : existing.Quantity)}.", "MEDIUM", fieldKey);

                                if (useNew) 
                                { 
                                    itemTracker[item.ItemId] = item; 
                                    context.RegisterResult(fieldKey, CreateExtractionResult(item), "XpItemNeuron_Conflict_New"); 
                                }
                            }
                            else if (item.Confidence > existing.Confidence)
                            {
                                itemTracker[item.ItemId] = item; 
                                context.RegisterResult(fieldKey, CreateExtractionResult(item), "XpItemNeuron_Confidence_Update");
                            }
                        }
                        else
                        {
                            itemTracker.Add(item.ItemId, item); 
                            context.RegisterResult(fieldKey, CreateExtractionResult(item), item.Strategy ?? "XpItemNeuron_Direct");
                        }
                    }
                }
                
                if (debugMode)
                {
                    context.RegisterMagnifierAttempt($"Image {imgIndex} XP Resolution", 1, $"Recovered {recoveredCount} items", recoveredCount > 0);
                }
                
                context.StopTimer($"Image_{imgIndex}_Logic");
            }
            catch (Exception ex)
            {
                context.LogError("XpOrchestrator", $"[System Error] Failed to process image {image.FileName}: {ex.Message}");
                _logger.LogError(ex, "Error processing XP image.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) 
                {
                    try { File.Delete(tempPath); } catch { }
                }
                context.StopTimer($"Image_{imgIndex}_Total");
            }
        }

        finalData.Items = itemTracker.Values.OrderByDescending(i => i.Confidence).ToList(); 

        context.StopTimer("TotalOrchestration");
        
        return (finalData, context);
    }

    private ExtractionResult<XpItemEntry> CreateExtractionResult(XpItemEntry item)
    {
        return new ExtractionResult<XpItemEntry>
        {
            Value = item,
            Confidence = item.Confidence,
            Strategy = item.Strategy ?? "Unknown",
            SourceBlock = item.AnchorBlock
        };
    }
}
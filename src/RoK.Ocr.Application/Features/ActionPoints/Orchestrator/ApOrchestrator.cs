using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Features.ActionPoints.Neurons;
using RoK.Ocr.Application.Features.ActionPoints.Services;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.ActionPoints;

namespace RoK.Ocr.Application.Features.ActionPoints.Orchestrator;

public class ApOrchestrator
{
    private readonly IOcrService _ocrService;
    private readonly IImageStorage _storage;
    private readonly ApMagnifier _magnifier;
    private readonly ILogger<ApOrchestrator> _logger;

    private readonly ApItemNeuron _itemNeuron = new();
    private readonly ApBarNeuron _barNeuron = new();

    public ApOrchestrator(
        IOcrService ocrService, 
        IImageStorage storage, 
        ApMagnifier magnifier,
        ILogger<ApOrchestrator> logger)
    {
        _ocrService = ocrService;
        _storage = storage;
        _magnifier = magnifier;
        _logger = logger;
    }

    public async Task<(ApInventoryData Data, string RawText)> ProcessInventoryAsync(List<IFormFile> images)
    {
        var finalData = new ApInventoryData();
        var itemTracker = new Dictionary<string, ApItemEntry>();
        var rawTextBuilder = new System.Text.StringBuilder();

        int imageIndex = 0;

        foreach (var imageFile in images)
        {
            imageIndex++;
            string tempPath = "";
            try
            {
                using (var stream = imageFile.OpenReadStream())
                {
                    tempPath = await _storage.SaveImageAsync(stream, imageFile.FileName);
                }

                // 2. OCR Python
                var (rawBlocks, fullText) = await _ocrService.AnalyzeInventoryAsync(tempPath);
                
                if (rawTextBuilder.Length > 0) rawTextBuilder.Append(" | ");
                rawTextBuilder.Append($"[IMG {imageIndex}] {fullText}");

                if (rawBlocks == null || !rawBlocks.Any()) continue;

                // 3. Classification
                var nodes = BlockClassifier.Classify(rawBlocks); 
                
                // Graph (1,1) -> Relative Geometry
                var graph = new TopologyGraph(nodes, 1, 1); 

                // 4. Bar Extraction
                var bar = _barNeuron.Extract(nodes);
                
                if (bar.Max > 0)
                {
                    if (finalData.MaxBarValue == 0)
                    {
                        finalData.CurrentBarValue = bar.Current;
                        finalData.MaxBarValue = bar.Max;
                    }
                    else if (finalData.MaxBarValue != bar.Max || finalData.CurrentBarValue != bar.Current)
                    {
                        finalData.Warnings.Add($"[AP Bar] Divergence detected in image {imageIndex}. " +
                                             $"Previous: {finalData.CurrentBarValue}/{finalData.MaxBarValue}, " +
                                             $"New: {bar.Current}/{bar.Max}. Kept previous value.");
                    }
                }

                // 5. Item Extraction
                var itemsFound = _itemNeuron.Extract(graph, nodes);

                // 6. Merge & Conflict
                foreach (var newItem in itemsFound)
                {
                    if (itemTracker.TryGetValue(newItem.ItemId, out var existingItem))
                    {
                        if (existingItem.Quantity != newItem.Quantity)
                        {
                            // --- CHANGE: Compare on 0-100 scale ---
                            // 5.0 represents 5% confidence difference
                            bool useNew = newItem.Confidence > existingItem.Confidence + 5.0; 
                            
                            // If diff is <= 5.0, assume similar confidence -> pick larger Quantity
                            if (Math.Abs(newItem.Confidence - existingItem.Confidence) <= 5.0)
                            {
                                useNew = newItem.Quantity > existingItem.Quantity;
                            }

                            string winnerVal = useNew ? newItem.Quantity.ToString() : existingItem.Quantity.ToString();
                            
                            // --- CHANGE: Format string for human readable percent ---
                            // Removed :P0 because the value is already 96.5, not 0.965
                            finalData.Warnings.Add($"[Item Conflict] '{newItem.Name}' diverged between images. " +
                                                 $"Values: {existingItem.Quantity} vs {newItem.Quantity}. " +
                                                 $"System chose: {winnerVal} (Conf: {newItem.Confidence}% vs {existingItem.Confidence}%).");

                            if (useNew)
                            {
                                itemTracker[newItem.ItemId] = newItem;
                            }
                        }
                        else
                        {
                            // Update if new read has better confidence
                            if (newItem.Confidence > existingItem.Confidence)
                            {
                                itemTracker[newItem.ItemId] = newItem;
                            }
                        }
                    }
                    else
                    {
                        itemTracker.Add(newItem.ItemId, newItem);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image {FileName}", imageFile.FileName);
                finalData.Warnings.Add($"[System Error] Failed to process image {imageFile.FileName}: {ex.Message}");
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
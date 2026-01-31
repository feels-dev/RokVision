using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Common.Dtos; 
using RoK.Ocr.Application.Common.Models;
using RoK.Ocr.Application.Features.ActionPoints.Neurons;
using RoK.Ocr.Application.Features.ActionPoints.Services;
using RoK.Ocr.Domain.Enums;
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

    public async Task<(ApInventoryData Data, OcrAnalysisContext Context)> ProcessInventoryAsync(List<IFormFile> images, bool debugMode = false)
    {
        var finalData = new ApInventoryData();
        var itemTracker = new Dictionary<string, ApItemEntry>();
        var context = new OcrAnalysisContext(); 
        context.StartTimer("TotalOrchestration"); 

        context.Log($"Starting AP inventory analysis for {images.Count} images.");

        int imageIndex = 0;

        foreach (var imageFile in images)
        {
            imageIndex++;
            context.StartTimer($"Image_{imageIndex}_Total"); 
            
            string tempPath = "";
            try
            {
                // 1. Save temporarily
                context.StartTimer($"Image_{imageIndex}_Storage");
                using (var stream = imageFile.OpenReadStream())
                {
                    tempPath = await _storage.SaveImageAsync(stream, imageFile.FileName);
                }
                context.StopTimer($"Image_{imageIndex}_Storage");

                // 2. Python OCR
                context.StartTimer($"Image_{imageIndex}_Python");
                var (rawBlocks, fullText) = await _ocrService.AnalyzeInventoryAsync(tempPath);
                context.StopTimer($"Image_{imageIndex}_Python");

                context.Log($"[IMG {imageIndex}] OCR Scan Complete. Found {rawBlocks?.Count ?? 0} blocks.");

                // Migrate Raw Text to Debug (if requested)
                if (debugMode && fullText.Length > 0)
                {
                    context.DebugInfo.RawText += $"--- IMG {imageIndex} ---\n{fullText}\n";
                }

                if (rawBlocks == null || !rawBlocks.Any()) continue;

                // 3. Classification
                context.StartTimer($"Image_{imageIndex}_Logic");
                var nodes = BlockClassifier.Classify(rawBlocks);

                // --- MAGNIFIER INTEGRATION START ---
                // Identify potential numbers with low confidence before extraction
                var riskyBlocks = nodes
                    .Where(n => n.Type == BlockType.Number && n.Raw.Confidence < 0.85)
                    .ToList();

                if (riskyBlocks.Any())
                {
                    context.Log($"[Magnifier] Identified {riskyBlocks.Count} low confidence blocks. Attempting repair...");
                    
                    // Call Magnifier
                    var improvedBlocks = await _magnifier.RescanQuantitiesAsync(tempPath, riskyBlocks, context);

                    int repairedCount = 0;
                    // Apply repairs to the nodes list
                    foreach (var improved in improvedBlocks)
                    {
                        // Find the corresponding node (simple matching by text or intersection could be better, 
                        // but here we rely on the list reference if we had the ID, 
                        // logic here assumes improvedBlocks maps back to riskyBlocks content)
                        
                        // Heuristic match based on box overlap since ID might change
                        var originalNode = riskyBlocks.FirstOrDefault(rb => 
                            CalculateOverlap(rb.Raw.Box, improved.Box) > 0.8);

                        if (originalNode != null && improved.Confidence > originalNode.Raw.Confidence)
                        {
                            context.Log($"[Magnifier] Repaired: '{originalNode.Raw.Text}' -> '{improved.Text}'");
                            originalNode.Raw.Text = improved.Text;
                            originalNode.Raw.Confidence = improved.Confidence;
                            repairedCount++;
                        }
                    }
                    context.RegisterMagnifierAttempt($"Image {imageIndex}", riskyBlocks.Count, $"Repaired {repairedCount}", repairedCount > 0);
                }
                // --- MAGNIFIER INTEGRATION END ---

                var graph = new TopologyGraph(nodes, 1, 1); 
                
                // 4. Bar Extraction (AP Bar)
                var barResult = _barNeuron.Extract(nodes); 

                if (barResult.Max > 0)
                {
                    if (finalData.MaxBarValue == 0)
                    {
                        finalData.CurrentBarValue = barResult.Current;
                        finalData.MaxBarValue = barResult.Max;
                        context.Log($"AP Bar initialized to {barResult.Current}/{barResult.Max}.");
                    }
                    else if (finalData.MaxBarValue != barResult.Max || finalData.CurrentBarValue != barResult.Current)
                    {
                        context.LogWarning("WARN_AP_BAR_DIVERGENCE",
                                             $"Divergence detected in image {imageIndex}. Prev: {finalData.CurrentBarValue}/{finalData.MaxBarValue}, New: {barResult.Current}/{barResult.Max}. Kept previous value.",
                                             "ap_bar");
                    }
                }

                // 5. Item Extraction
                // Now extracts using potentially corrected numbers
                var itemsFound = _itemNeuron.Extract(graph, nodes);
                context.Log($"[IMG {imageIndex}] Extracted {itemsFound.Count} potential items.");

                // 6. Merge & Conflict
                foreach (var newItem in itemsFound)
                {
                    string fieldKey = $"item_{newItem.ItemId}";

                    if (itemTracker.TryGetValue(newItem.ItemId, out var existingItem))
                    {
                        if (existingItem.Quantity != newItem.Quantity)
                        {
                            bool useNew = newItem.Confidence > existingItem.Confidence + 5.0;
                            if (Math.Abs(newItem.Confidence - existingItem.Confidence) <= 5.0) useNew = newItem.Quantity > existingItem.Quantity;

                            string winnerName = useNew ? "New Item" : "Existing Item";
                            context.LogWarning("WARN_ITEM_CONFLICT",
                                $"Item '{newItem.Name}' conflict. Values: {existingItem.Quantity} vs {newItem.Quantity}. Chosen: {winnerName} (Conf: {newItem.Confidence}% vs {existingItem.Confidence}%).",
                                fieldKey);

                            if (useNew)
                            {
                                itemTracker[newItem.ItemId] = newItem;
                                context.RegisterResult(fieldKey, CreateExtractionResult(newItem), "ApItemNeuron_Conflict_New");
                            }
                        }
                        else
                        {
                            if (newItem.Confidence > existingItem.Confidence)
                            {
                                itemTracker[newItem.ItemId] = newItem;
                                context.RegisterResult(fieldKey, CreateExtractionResult(newItem), "ApItemNeuron_Confidence_Update");
                            }
                        }
                    }
                    else
                    {
                        itemTracker.Add(newItem.ItemId, newItem);
                        context.RegisterResult(fieldKey, CreateExtractionResult(newItem), "ApItemNeuron_New");
                    }
                }
                context.StopTimer($"Image_{imageIndex}_Logic");
            }
            catch (Exception ex)
            {
                context.LogError($"[System Error] Failed to process image {imageFile.FileName}: {ex.Message}");
                _logger.LogError(ex, "Error processing image {FileName}", imageFile.FileName);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath)) _storage.DeleteImage(tempPath);
                context.StopTimer($"Image_{imageIndex}_Total");
            }
        }

        finalData.Items = itemTracker.Values.OrderBy(i => i.UnitValue).ToList();

        context.StopTimer("TotalOrchestration");
        context.AuditLog.Add($"[INFO] Final AP Inventory Confidence: {finalData.Items.Average(i => i.Confidence):F2}%");

        return (finalData, context);
    }

    // Helper for box overlap calculation
    private double CalculateOverlap(List<List<double>> box1, List<List<double>> box2)
    {
        // Simple AABB overlap check logic for matching
        double x1 = Math.Max(box1[0][0], box2[0][0]);
        double y1 = Math.Max(box1[0][1], box2[0][1]);
        double x2 = Math.Min(box1[2][0], box2[2][0]);
        double y2 = Math.Min(box1[2][1], box2[2][1]);

        if (x2 < x1 || y2 < y1) return 0.0;
        return (x2 - x1) * (y2 - y1);
    }

    private ExtractionResult<ApItemEntry> CreateExtractionResult(ApItemEntry item)
    {
        return new ExtractionResult<ApItemEntry>
        {
            Value = item,
            Confidence = item.Confidence,
            SourceBlock = null 
        };
    }
}
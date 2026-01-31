using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Experience;
using RoK.Ocr.Application.Common.Models; // Important: OcrAnalysisContext

namespace RoK.Ocr.Application.Features.Experience.Services;

public class XpMagnifier
{
    private readonly IOcrService _ocrService;
    private readonly ILogger<XpMagnifier> _logger;

    public XpMagnifier(IOcrService ocrService, ILogger<XpMagnifier> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    /// <summary>
    /// Rescans low-confidence or missing quantity items using Batch OCR with focused strategies.
    /// </summary>
    public async Task ResolveMissingQuantitiesAsync(
        string imagePath, 
        List<XpItemEntry> incompleteItems, 
        OcrAnalysisContext context) // Injected Context
    {
        context.StartTimer("XpMagnifierBatch"); // Start Timer

        var targets = incompleteItems.Where(i => i.Quantity == -1 && i.AnchorBlock != null).ToList();
        if (!targets.Any()) 
        {
            context.StopTimer("XpMagnifierBatch");
            return;
        }

        // --- DYNAMIC: LOCAL RULER CALCULATION ---
        var heights = targets.Select(t => t.AnchorBlock!.Raw.Box[2][1] - t.AnchorBlock!.Raw.Box[0][1]).OrderBy(h => h).ToList();
        double medianH = heights.Count > 0 ? heights[heights.Count / 2] : 20.0; // Safe fallback

        var requestMap = new Dictionary<string, XpItemEntry>();
        var batchRequests = new List<(string Id, int[] Box, string Strategy)>();

        foreach (var item in targets)
        {
            var box = item.AnchorBlock!.Raw.Box;
            
            // Anchor Center
            double centerX = (box[0][0] + box[2][0]) / 2;
            double bottomY = box[2][1];

            // --- ADAPTIVE GEOMETRY (Based on Average) ---

            // Y: Start slightly above text bottom (overlap)
            int cropY = (int)(bottomY - (medianH * 0.2));
            
            // Proportional Fixed Height: 4x letter height
            int cropH = (int)(medianH * 4.5);

            // SHOT 1: Centered (Wide)
            int cropW_1 = (int)(medianH * 10.0);
            int cropX_1 = (int)(centerX - (cropW_1 / 2));

            // SHOT 2: Right Focus (For numbers like "1,000")
            int cropW_2 = (int)(medianH * 6.0);
            int cropX_2 = (int)(centerX); 

            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            requestMap[id1] = item;
            requestMap[id2] = item;

            // Send WhiteIsolation (Better for colored icons)
            batchRequests.Add((id1, new[] { cropX_1, cropY, cropW_1, cropH }, "WhiteIsolation"));
            batchRequests.Add((id2, new[] { cropX_2, cropY, cropW_2, cropH }, "WhiteIsolation"));
        }

        context.Log($"[XpMagnifier] Disparando {batchRequests.Count} rescans adaptativos para {targets.Count} itens XP pendentes.");

        var results = await _ocrService.AnalyzeBatchAsync(imagePath, batchRequests);

        foreach (var res in results)
        {
            if (requestMap.TryGetValue(res.CustomId, out var item))
            {
                string clean = new string(res.Text.Where(char.IsDigit).ToArray());
                
                if (int.TryParse(clean, out int qty) && qty > 0)
                {
                    // Confidence Logic: Stricter for small numbers
                    double threshold = qty < 10 ? 0.15 : 0.40;

                    if (res.Confidence > threshold)
                    {
                        double newConf = Math.Round(res.Confidence * 100, 2);

                        // Priority Logic
                        if (item.Quantity == -1)
                        {
                            item.Quantity = qty;
                            item.Confidence = newConf;
                            item.DetectedColor = item.DetectedColor.Replace("_PENDING", "");
                            context.Log($"[Magnifier HIT] Resolved {item.ItemId} to {qty} ({newConf:F2}%)");
                        }
                        else if (qty > item.Quantity)
                        {
                            item.Quantity = qty;
                            item.Confidence = newConf;
                            context.Log($"[Magnifier UPDATE] {item.ItemId}: {item.Quantity} -> {qty} (Higher Qty)");
                        }
                        else if (qty == item.Quantity && newConf > item.Confidence)
                        {
                            item.Confidence = newConf;
                        }
                    }
                }
            }
        }
        
        context.StopTimer("XpMagnifierBatch"); // Stop timer
    }
}
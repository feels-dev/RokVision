using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; 
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Application.Common.Models;

namespace RoK.Ocr.Application.Features.Reports.Services;

public class WarMagnifier
{
    private readonly IOcrService _ocrService;
    private readonly ILogger<WarMagnifier> _logger; 

    public WarMagnifier(IOcrService ocrService, ILogger<WarMagnifier> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    /// <summary>
    /// BATCH MODE: Prepares a list of regions to be rescanned with specific strategies
    /// and sends them all to Python in a SINGLE HTTP request.
    /// </summary>
    public async Task<List<OcrBlock>> RescanBatchAsync(
        string imagePath, 
        List<AnalyzedBlock> nodesToRepair, 
        OcrAnalysisContext context) // Contexto injetado
    {
        if (nodesToRepair == null || !nodesToRepair.Any()) 
            return new List<OcrBlock>();

        // 1. Prepare Batch Requests
        var batchRequests = new List<(string Id, int[] Box, string Strategy)>();
        var strategies = new[] { "HighContrastBinary", "Sharpen", "InvertedBinary" };

        foreach (var node in nodesToRepair)
        {
            // Box structure: [[x1,y1], [x2,y1], [x2,y2], [x1,y2]]
            int x = (int)node.Raw.Box[0][0];
            int y = (int)node.Raw.Box[0][1];
            int w = (int)(node.Raw.Box[2][0] - node.Raw.Box[0][0]);
            int h = (int)(node.Raw.Box[2][1] - node.Raw.Box[0][1]);
            
            x = Math.Max(0, x - 10);
            y = Math.Max(0, y - 8);
            w += 25; 
            h += 16;

            foreach (var strat in strategies)
            {
                string uniqueId = $"{node.Raw.Text}_{strat}_{Guid.NewGuid().ToString().Substring(0,4)}"; 
                batchRequests.Add((uniqueId, new[] { x, y, w, h }, strat));
            }
        }

        // 2. ONE CALL TO RULE THEM ALL
        context.Log($"[WarMagnifier] Sending Batch Request: {batchRequests.Count} sub-tasks for {nodesToRepair.Count} nodes.");
        
        var results = await _ocrService.AnalyzeBatchAsync(imagePath, batchRequests);

        // 3. Filter Results
        var validResults = results
            .Where(r => r.Confidence > 0.60)
            .ToList();

        context.Log($"[WarMagnifier] Batch finished. Received {validResults.Count} valid candidates.");

        return validResults;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; 
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;

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
    public async Task<List<OcrBlock>> RescanBatchAsync(string imagePath, List<AnalyzedBlock> nodesToRepair)
    {
        if (nodesToRepair == null || !nodesToRepair.Any()) 
            return new List<OcrBlock>();

        // 1. Prepare Batch Requests
        // We ask Python to try multiple strategies for EACH node to maximize success chance.
        var batchRequests = new List<(string Id, int[] Box, string Strategy)>();

        // Strategies defined in Python's ImageProcessor
        var strategies = new[] { "HighContrastBinary", "Sharpen", "InvertedBinary" };

        foreach (var node in nodesToRepair)
        {
            // Convert Box coordinates to integer [x, y, w, h] format
            // Box structure: [[x1,y1], [x2,y1], [x2,y2], [x1,y2]]
            int x = (int)node.Raw.Box[0][0];
            int y = (int)node.Raw.Box[0][1];
            int w = (int)(node.Raw.Box[2][0] - node.Raw.Box[0][0]);
            int h = (int)(node.Raw.Box[2][1] - node.Raw.Box[0][1]);
            
            // Add safety padding (margin) to ensure we capture the full number
            x = Math.Max(0, x - 10);
            y = Math.Max(0, y - 8);
            w += 25; // Extra width is safe
            h += 16;

            foreach (var strat in strategies)
            {
                // Unique ID format: "OriginalText_Strategy" 
                // (Used to track back results if needed, though we filter by confidence)
                string uniqueId = $"{node.Raw.Text}_{strat}_{Guid.NewGuid().ToString().Substring(0,4)}"; 
                
                batchRequests.Add((uniqueId, new[] { x, y, w, h }, strat));
            }
        }

        // 2. ONE CALL TO RULE THEM ALL (Network Optimization)
        _logger.LogInformation("[WarMagnifier] Sending Batch Request: {Count} sub-tasks for {NodeCount} nodes.", 
            batchRequests.Count, nodesToRepair.Count);
        
        // This calls the Python /batch/process endpoint
        var results = await _ocrService.AnalyzeBatchAsync(imagePath, batchRequests);

        // 3. Process Results
        // We filter out garbage results immediately.
        // The Orchestrator will then match these potential good numbers against the bad original nodes.
        var validResults = results
            .Where(r => r.Confidence > 0.60) // Minimum confidence threshold
            .ToList();

        _logger.LogInformation("[WarMagnifier] Batch finished. Received {Count} valid candidates.", validResults.Count);

        return validResults;
    }
}
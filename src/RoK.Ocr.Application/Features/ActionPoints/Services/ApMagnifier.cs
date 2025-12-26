using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.ActionPoints.Services;

public class ApMagnifier
{
    private readonly IOcrService _ocrService;
    private readonly ILogger<ApMagnifier> _logger;

    public ApMagnifier(IOcrService ocrService, ILogger<ApMagnifier> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<List<OcrBlock>> RescanQuantitiesAsync(string imagePath, List<AnalyzedBlock> lowConfidenceBlocks)
    {
        if (!lowConfidenceBlocks.Any()) return new List<OcrBlock>();

        var batchRequests = new List<(string Id, int[] Box, string Strategy)>();

        foreach (var block in lowConfidenceBlocks)
        {
            int x = (int)block.Raw.Box[0][0];
            int y = (int)block.Raw.Box[0][1];
            int w = (int)(block.Raw.Box[2][0] - block.Raw.Box[0][0]);
            int h = (int)(block.Raw.Box[2][1] - block.Raw.Box[0][1]);

            // Expand box slightly for better context
            x = Math.Max(0, x - 5);
            w += 15;

            batchRequests.Add(($"{block.Raw.Text}_std", new[] { x, y, w, h }, "Sharpen"));
            batchRequests.Add(($"{block.Raw.Text}_bin", new[] { x, y, w, h }, "HighContrastBinary"));
        }

        _logger.LogInformation("[ApMagnifier] Sending {Count} regions for re-analysis.", batchRequests.Count);

        var results = await _ocrService.AnalyzeBatchAsync(imagePath, batchRequests);

        return results
            .Where(r => r.Confidence > 0.75 && r.Text.Any(char.IsDigit))
            .ToList();
    }
}
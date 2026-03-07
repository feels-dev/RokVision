using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Interfaces;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.Map.Neurons;

/// <summary>
/// Neuron responsible for extracting Kingdom Number and X/Y Coordinates.
/// Optimized with Source Generators and Normalized Coordinates for resolution independence.
/// </summary>
public partial class CoordinateNeuron : IOcrNeuron<(int K, int X, int Y)>
{
    // High-Performance Compiled Regex
    [GeneratedRegex(@"#\s*(\d{4,})", RegexOptions.Compiled)]
    private static partial Regex KingdomRegex();

    [GeneratedRegex(@"X[:;\s]*(\d{1,4})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex XCoordRegex();

    [GeneratedRegex(@"Y[:;\s]*(\d{1,4})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex YCoordRegex();

    public ExtractionResult<(int K, int X, int Y)> Process(
        List<AnalyzedBlock> allBlocks, 
        Dictionary<string, AnalyzedBlock> anchors, 
        List<AnalyzedBlock> blacklist)
    {
        int k = 0, x = 0, y = 0;
        AnalyzedBlock? sourceBlock = null;

        // Optimization: Scan only the top 20% of the screen (Normalized Y < 0.20).
        // This ensures it works on 720p, 1080p, 1440p, and 4K images equally well.
        var topBlocks = allBlocks
            .Where(b => b.NormalizedCenter.Y < 0.20) 
            .ToList();

        foreach (var block in topBlocks)
        {
            string text = block.Raw.Text.ToUpper();

            // Extract Kingdom Number (e.g., #3746)
            var kMatch = KingdomRegex().Match(text);
            if (kMatch.Success)
            {
                if (int.TryParse(kMatch.Groups[1].Value, out int val)) k = val;
                // We prefer the block containing the Kingdom ID as the primary source anchor
                sourceBlock = block;
            }

            // Extract X Coordinate
            var xMatch = XCoordRegex().Match(text);
            if (xMatch.Success) int.TryParse(xMatch.Groups[1].Value, out x);

            // Extract Y Coordinate
            var yMatch = YCoordRegex().Match(text);
            if (yMatch.Success) int.TryParse(yMatch.Groups[1].Value, out y);
        }

        // We require at least valid coordinates to consider it a success.
        // Kingdom ID might sometimes be obscured by UI, but coords are essential for mapping.
        if (x > 0 && y > 0)
        {
            return new ExtractionResult<(int K, int X, int Y)>
            {
                Value = (k, x, y),
                Confidence = sourceBlock?.Raw.Confidence * 100 ?? 90, 
                SourceBlock = sourceBlock,
                Strategy = "Regex_NormalizedHeader"
            };
        }

        return new ExtractionResult<(int K, int X, int Y)> 
        { 
            Value = (0, 0, 0), 
            Confidence = 0,
            Strategy = "NotFound"
        };
    }
}
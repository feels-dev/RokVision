using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Interfaces;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.Map.Neurons;

/// <summary>
/// Neuron responsible for extracting Kingdom Number and X/Y Coordinates.
/// </summary>
public class CoordinateNeuron : IOcrNeuron<(int K, int X, int Y)>
{
    public ExtractionResult<(int K, int X, int Y)> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        int k = 0, x = 0, y = 0;
        AnalyzedBlock? sourceBlock = null;

        // Optimization: Only scan the top 20% of the screen (coordinates are always at the top)
        // Assuming typical 1080p height, < 300px is safe
        var topBlocks = allBlocks
            .Where(b => b.Raw.Center.Y < 300) 
            .ToList();

        foreach (var block in topBlocks)
        {
            string text = block.Raw.Text.ToUpper();

            // Regex for Kingdom Number (e.g., #3746)
            var kMatch = Regex.Match(text, @"#\s*(\d{4,})");
            if (kMatch.Success)
            {
                if (int.TryParse(kMatch.Groups[1].Value, out int val)) k = val;
                sourceBlock = block;
            }

            // Regex for X Coordinate
            var xMatch = Regex.Match(text, @"X[:;\s]*(\d{1,4})");
            if (xMatch.Success) int.TryParse(xMatch.Groups[1].Value, out x);

            // Regex for Y Coordinate
            var yMatch = Regex.Match(text, @"Y[:;\s]*(\d{1,4})");
            if (yMatch.Success) int.TryParse(yMatch.Groups[1].Value, out y);
        }

        // We only consider it a success if at least X and Y are found
        if (x > 0 && y > 0)
        {
            return new ExtractionResult<(int K, int X, int Y)>
            {
                Value = (k, x, y),
                Confidence = 90, // High confidence due to regex match
                SourceBlock = sourceBlock
            };
        }

        return new ExtractionResult<(int K, int X, int Y)> { Value = (0, 0, 0), Confidence = 0 };
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Application.Common.Interfaces;

namespace RoK.Ocr.Application.Features.Governor.Neurons;

/// <summary>
/// Specialized neuron for identifying the unique Governor ID.
/// Implements a strict priority for labeled IDs to avoid misreading HUD Power values.
/// </summary>
public partial class IdNeuron : IOcrNeuron<int>
{
    // Regex optimized to capture digits 7-10 after common ID labels
    [GeneratedRegex(@"(ID|1D|lD|Id|id)\s*[:).]?\s*(?<number>\d{7,10})", RegexOptions.Compiled)]
    private static partial Regex IdStrictRegex();

    public ExtractionResult<int> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        // 1. HIGH PRIORITY: Look for blocks containing the literal "ID" label
        var strictMatch = allBlocks
            .Where(b => b.Raw.Text.Contains("ID", StringComparison.OrdinalIgnoreCase) ||
                        b.Raw.Text.Contains("Governador", StringComparison.OrdinalIgnoreCase))
            .Select(b =>
            {
                var match = IdStrictRegex().Match(b.Raw.Text);
                return new { Match = match, Block = b };
            })
            .Where(x => x.Match.Success)
            .OrderByDescending(x => x.Block.Raw.Confidence)
            .FirstOrDefault();

        if (strictMatch != null)
        {
            // Dynamically calculate confidence based on the OCR engine's confidence
            double rawConf = strictMatch.Block.Raw.Confidence > 1 ? strictMatch.Block.Raw.Confidence : strictMatch.Block.Raw.Confidence * 100;
            
            return new ExtractionResult<int>
            {
                Value = int.Parse(strictMatch.Match.Groups["number"].Value),
                Confidence = rawConf > 0 ? rawConf : 98, // Fallback if raw is 0
                Strategy = "IdNeuron_StrictLabel",
                SourceBlock = strictMatch.Block
            };
        }

        // 2. FALLBACK: Look for pure numeric strings if no labeled ID is found
        var fallback = allBlocks
            .Except(blacklist)
            .Where(b => b.Type == BlockType.Number || b.Type == BlockType.Unknown)
            .Where(b => b.Raw.Center.Y > 100) // HUD Power/Recourse exclusion zone
            .Select(b => new { Val = ExtractDigits(b.Raw.Text), Block = b })
            .Where(x => x.Val > 10_000_000) // RoK IDs are typically 8+ digits
            .OrderBy(x => x.Block.Raw.Center.Y) // Pick the first candidate below the HUD
            .FirstOrDefault();

        if (fallback != null)
        {
            double rawConf = fallback.Block.Raw.Confidence > 1 ? fallback.Block.Raw.Confidence : fallback.Block.Raw.Confidence * 100;
            
            return new ExtractionResult<int> 
            { 
                Value = fallback.Val, 
                Confidence = rawConf > 0 ? rawConf : 60,
                Strategy = "IdNeuron_SpatialFallback",
                SourceBlock = fallback.Block 
            };
        }

        return new ExtractionResult<int> { Value = 0, Confidence = 0, Strategy = "Failed" };
    }

    private int ExtractDigits(string text)
    {
        var digits = Regex.Replace(text, @"[^\d]", "");
        return int.TryParse(digits, out int id) ? id : 0;
    }
}
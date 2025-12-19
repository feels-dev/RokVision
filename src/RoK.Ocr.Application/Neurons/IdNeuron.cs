using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Neurons;

public class IdNeuron : IOcrNeuron<int>
{
    public ExtractionResult<int> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        // 1. Looks for blocks already classified as ID (High Confidence)
        var idBlock = allBlocks
            .Except(blacklist)
            .FirstOrDefault(b => b.Type == BlockType.ID);

        if (idBlock != null)
        {
            return new ExtractionResult<int>
            {
                Value = ExtractId(idBlock.Raw.Text),
                Confidence = 95,
                SourceBlock = idBlock
            };
        }

        // 2. Smart Fallback: Searches the top half of the screen (Y < 600)
        // Searches for patterns that look like IDs (7 to 10 digits)
        var candidates = allBlocks
            .Except(blacklist)
            .Where(b => b.Type == BlockType.Unknown || b.Type == BlockType.Number) // Might have been classified as a number
            .Where(b => b.Center.Y < 600)
            .Select(b => new { Val = ExtractId(b.Raw.Text), Block = b })
            .Where(x => x.Val > 1_000_000 && x.Val < 2_000_000_000) // RoK IDs are typically in this range
            .OrderByDescending(x => x.Block.Raw.Text.Contains("ID", StringComparison.OrdinalIgnoreCase)) // Prioritizes if it contains "ID" text
            .FirstOrDefault();

        if (candidates != null)
        {
            return new ExtractionResult<int>
            {
                Value = candidates.Val,
                Confidence = 70,
                SourceBlock = candidates.Block
            };
        }

        return new ExtractionResult<int> { Value = 0, Confidence = 0 };
    }

    private int ExtractId(string text)
    {
        // SURGICAL CLEANING:
        string clean = Regex.Replace(text, @"(ID|1D|lD|Id|id)\s*[:\)\.]?\s*", "");
        var digits = Regex.Match(clean, @"\d{7,10}");
        if (digits.Success && int.TryParse(digits.Value, out int id))
        {
            return id;
        }
        return 0;
    }
}
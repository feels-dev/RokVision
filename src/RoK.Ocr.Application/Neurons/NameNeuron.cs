using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Cognitive;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Neurons;

public class NameNeuron : IOcrNeuron<string>
{
    public ExtractionResult<string> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        // Without an ID, the neuron is blind.
        if (!anchors.ContainsKey("ID")) 
            return new ExtractionResult<string> { Value = "--", Confidence = 0 };

        var idAnchor = anchors["ID"];

        // CANDIDATES: 
        // 1. Not in the blacklist
        // 2. Are of type Unknown (or Tag, sometimes the name sticks to the tag)
        // 3. Have at least 3 characters
        var candidates = allBlocks
            .Except(blacklist)
            .Where(b => b.Type == BlockType.Unknown || b.Type == BlockType.Tag) 
            .Where(b => b.Raw.Text.Length >= 3)
            .Select(b => new 
            { 
                Block = b, 
                Score = CalculateScore(b, idAnchor, allBlocks) 
            })
            .Where(x => x.Score > 0) // Only accepts positive scores
            .OrderByDescending(x => x.Score)
            .ToList();

        var winner = candidates.FirstOrDefault();

        if (winner != null)
        {
            return new ExtractionResult<string>
            {
                Value = CleanName(winner.Block.Raw.Text),
                Confidence = winner.Score,
                SourceBlock = winner.Block
            };
        }

        return new ExtractionResult<string> { Value = "--", Confidence = 0 };
    }

    private double CalculateScore(AnalyzedBlock candidate, AnalyzedBlock idAnchor, List<AnalyzedBlock> allBlocks)
    {
        double score = 100.0;
        var b = candidate.Raw;

        // --- STRICT GEOMETRIC ANALYSIS (The "Invisible Wall") ---

        // 1. Vertical Difference (Y)
        // The name must be BELOW the ID, but not too much.
        // ID.Y < Name.Y < ID.Y + 200px (Safe Zone)
        double diffY = b.Center.Y - idAnchor.Center.Y;

        if (diffY < -20) return -999; // Is ABOVE the ID (e.g., Power at the top) -> Trash
        if (diffY > 200) return -999; // Is TOO FAR BELOW (e.g., BHC in the footer) -> Nuclear Trash

        // 2. Horizontal Difference (X)
        // The name must start or be centered near the ID.
        double diffX = Math.Abs(b.Center.X - idAnchor.Center.X);
        if (diffX > 300) score -= 50; // Is too far horizontally

        // --- SEMANTIC ANALYSIS ---

        // 3. Penalty if it contains the ID number (OCR read a duplicate)
        if (b.Text.Contains(ParseInt(idAnchor.Raw.Text).ToString())) return -999;

        // 4. UI Penalty (Extra Verification)
        if (IsUiKeyword(b.Text)) return -999; // Kills "2 Wins"

        // 5. Nuclear Penalty: Fractional Neighbor (The "Action Points" case)
        if (HasStatusNeighbor(candidate, allBlocks)) return -999;

        // 6. Bonus: Golden Zone
        // If it is right below (0 to 120px) and aligned
        if (diffY > 0 && diffY < 120 && diffX < 150) score += 50;

        return score;
    }

    private bool HasStatusNeighbor(AnalyzedBlock target, List<AnalyzedBlock> all)
    {
        // Looks for bars like "1500/1500" on the same line
        return all.Any(other => 
            other.Type == BlockType.BarStatus && 
            Math.Abs(other.Center.Y - target.Center.Y) < 30 &&
            Math.Abs(other.Center.X - target.Center.X) < 450
        );
    }

    private bool IsUiKeyword(string text)
    {
        foreach (var key in RokVocabulary.UiKeywords)
        {
            // High similarity
            if (RokCognitiveTools.CalculateSimilarity(text, key) > 0.8) return true;
            // Contains the word (e.g., "2 Wins" contains "Wins")
            if (text.Contains(key, StringComparison.InvariantCultureIgnoreCase)) return true;
        }
        return false;
    }

    private string CleanName(string text) => Regex.Replace(text, @"[^\w\s\-\[\]]", "").Trim();
    private int ParseInt(string text) => int.TryParse(Regex.Replace(text, @"[^0-9]", ""), out int v) ? v : 0;
}
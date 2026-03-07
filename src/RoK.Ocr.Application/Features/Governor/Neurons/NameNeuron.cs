using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Common.Interfaces;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.Governor.Neurons;

/// <summary>
/// Specialized neuron for identifying the Governor's Name.
/// Uses spatial heuristics and scoring to distinguish the name from UI labels.
/// </summary>
public class NameNeuron : IOcrNeuron<string>
{
    public ExtractionResult<string> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        // The ID anchor is mandatory to define the search context
        if (!anchors.ContainsKey("ID"))
            return new ExtractionResult<string> { Value = "--", Confidence = 0, Strategy = "Name_NoAnchor" };

        var idAnchor = anchors["ID"];

        // Filter and score potential candidates
        var candidates = allBlocks
            .Except(blacklist)
            // Names usually fall into Unknown (generic text) or Tag (if text is attached to [TAG])
            .Where(b => b.Type == BlockType.Unknown || b.Type == BlockType.Tag)
            .Where(b => b.Raw.Text.Length >= 3)
            .Select(b => new
            {
                Block = b,
                Score = CalculateScore(b, idAnchor, allBlocks)
            })
            .Where(x => x.Score > 0) // Discard negative scores (invalid candidates)
            .OrderByDescending(x => x.Score)
            .ToList();

        var winner = candidates.FirstOrDefault();

        if (winner != null)
        {
            return new ExtractionResult<string>
            {
                Value = CleanName(winner.Block.Raw.Text),
                Confidence = winner.Score,
                Strategy = "Name_SpatialScore",
                SourceBlock = winner.Block
            };
        }

        return new ExtractionResult<string> { Value = "--", Confidence = 0, Strategy = "Name_NotFound" };
    }

    /// <summary>
    /// Calculates a score for a candidate block based on spatial and semantic rules.
    /// </summary>
    private double CalculateScore(AnalyzedBlock candidate, AnalyzedBlock idAnchor, List<AnalyzedBlock> allBlocks)
    {
        string text = candidate.Raw.Text.Trim();
        var raw = candidate.Raw;

        // --- 1. CORE FILTERS (Immediate Elimination) ---
        if (IsUiKeyword(text)) return -999;
        if (text.Length < 3) return -999;
        if (Regex.IsMatch(text, @"^\d+[\.,]?\d*$")) return -999;

        string cleanId = Regex.Replace(idAnchor.Raw.Text, @"[^\d]", "");
        if (!string.IsNullOrEmpty(cleanId) && text.Contains(cleanId)) return -999;

        double score = 100.0;

        // --- 2. GEOMETRIC ANALYSIS (Relative Positioning) ---
        double diffY = raw.Center.Y - idAnchor.Center.Y;
        double diffX = Math.Abs(raw.Center.X - idAnchor.Center.X);

        if (diffY < -25) return -999;
        if (diffY > 180) return -999;
        if (diffX > 400) score -= 60;

        // --- 3. SEMANTIC BONUSES AND PENALTIES ---
        if (text.Contains("[") || text.Contains("]")) score += 50;

        if (diffY > 20 && diffY < 80 && diffX < 150) score += 40;

        bool hasStatusBarNearby = allBlocks.Any(other =>
            other.Type == BlockType.BarStatus &&
            Math.Abs(other.Center.Y - candidate.Center.Y) < 40
        );

        if (hasStatusBarNearby) score -= 50;

        int specialChars = text.Count(c => !char.IsLetterOrDigit(c) && c != '[' && c != ']' && c != ' ');
        if (specialChars > 3) score -= 30;

        // --- 4. OCR CONFIDENCE ---
        score += (candidate.Raw.Confidence * 20);

        return score;
    }

    private bool IsUiKeyword(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        string cleanText = text.Replace(":", "").Replace(".", "").Trim();
        var allKeys = RokVocabulary.UiKeywords
            .Concat(RokVocabulary.StatusLabels)
            .Concat(RokVocabulary.GovernorLabels)
            .Concat(RokVocabulary.AllianceLabels)
            .Concat(RokVocabulary.PowerLabels);

        foreach (var key in allKeys)
        {
            if (cleanText.Contains(key, StringComparison.OrdinalIgnoreCase)) return true;
            if (RokCognitiveTools.CalculateSimilarity(cleanText, key) > 0.75) return true;
        }
        return false;
    }

    private string CleanName(string text) => Regex.Replace(text, @"[^\w\s\-\[\]]", "").Trim();
}
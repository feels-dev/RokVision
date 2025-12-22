using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Cognitive;
using RoK.Ocr.Application.Reports.Constants;
using RoK.Ocr.Application.Shared.Cognitive;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Reports.Cognitive;

public static class WarBlockClassifier
{
    public static void ClassifyNodes(List<AnalyzedBlock> nodes)
    {
        foreach (var node in nodes)
        {
            string text = node.Raw.Text.Trim();

            // 1. TOP PRIORITY: Mapping of Metric Labels and Results
            // We check this first because if the block contains "Units | 40.342", 
            // it MUST be UnitsLabel for the Graph to work correctly.

            if (MatchPartial(text, WarVocabulary.UnitsLabels))
                node.Type = BlockType.UnitsLabel;

            else if (MatchPartial(text, WarVocabulary.DeadLabels))
                node.Type = BlockType.DeadLabel;

            else if (MatchPartial(text, WarVocabulary.SevereWoundedLabels))
                node.Type = BlockType.SevereWoundedLabel;

            else if (MatchPartial(text, WarVocabulary.SlightlyWoundedLabels))
                node.Type = BlockType.SlightlyWoundedLabel;

            else if (MatchPartial(text, WarVocabulary.RemainingLabels))
                node.Type = BlockType.RemainingLabel;

            else if (MatchPartial(text, WarVocabulary.HealedLabels))
                node.Type = BlockType.HealedLabel;

            else if (MatchPartial(text, WarVocabulary.WatchtowerLabels))
                node.Type = BlockType.WatchtowerLabel;

            else if (MatchPartial(text, WarVocabulary.KillPointsLabels))
                node.Type = BlockType.KillPointsLabel;

            else if (MatchPartial(text, WarVocabulary.VictoryTerms) || MatchPartial(text, WarVocabulary.DefeatTerms))
                node.Type = BlockType.StatusResult;

            // 2. ALLIANCE TAGS
            // If it starts with [ or contains the brackets pattern, we mark it as Tag.
            else if (Regex.IsMatch(text, @"^\[.+") || (text.Contains("[") && text.Contains("]")))
            {
                node.Type = BlockType.Tag;
            }

            // 3. PURE NUMBERS
            // If the block is purely numeric (e.g., "40.342"), we mark it as Number.
            // The MetricNeuron will use these blocks to populate the values for the labels above.
            else if (IsPureNumeric(text))
            {
                node.Type = BlockType.Number;
            }

            // 4. CONTEXT METADATA (DATE / TIME / COORDINATES)
            else if (Regex.IsMatch(text, @"\d{2}:\d{2}|UTC|\d{2}/\d{2}"))
            {
                node.Type = BlockType.DateOrTime;
            }
            else if (Regex.IsMatch(text, @"X:?\s*\d+.*Y:?\s*\d+", RegexOptions.IgnoreCase))
            {
                node.Type = BlockType.DateOrTime;
            }

            // 5. UI FILTER (Blacklist)
            // We only check the Blacklist down here. If the text is "Heal", 
            // it was already caught in step 1 and will not enter here as UI.
            else if (WarVocabulary.GlobalBlacklist.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase)) ||
                     WarVocabulary.UiBlacklist.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase)))
            {
                node.Type = BlockType.UI;
            }

            // 6. UNKNOWN (Candidates for Governor Name or Commanders)
            else
            {
                node.Type = BlockType.Unknown;
            }
        }
    }

    private static bool MatchPartial(string text, string[] vocabulary)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var v in vocabulary)
        {
            // Checks if the vocabulary word is contained in the text (ignores case)
            if (text.Contains(v, StringComparison.OrdinalIgnoreCase)) return true;

            // Checks similarity (Fuzzy) for cases where OCR misses a letter
            // We compare only the first part before the pipe | in case OCR stuck the number to it
            string firstPart = text.Split('|')[0].Trim();
            if (RokCognitiveTools.CalculateSimilarity(firstPart, v) > 0.85) return true;
        }

        return false;
    }

    private static bool IsPureNumeric(string text)
    {
        // Remove dot and comma for "Purity" test
        string clean = text.Replace(".", "").Replace(",", "").Replace(" ", "").Replace("+", "").Trim();

        // If after cleaning only digits and maybe a K or M remain, it is a number
        return Regex.IsMatch(clean, @"^\d+[KM]?$");
    }
}
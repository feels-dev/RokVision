using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Common.Cognitive;

/// <summary>
/// Categorizes raw OCR blocks into specific game types (UI, ID, Power, etc.)
/// based on patterns and vocabularies.
/// </summary>
public static class BlockClassifier
{
    public static List<AnalyzedBlock> Classify(List<OcrBlock> rawBlocks)
    {
        var list = new List<AnalyzedBlock>();
        foreach (var block in rawBlocks)
        {
            var analysis = new AnalyzedBlock { Raw = block };
            string text = block.Text.Trim();

            // 1. UI Keywords: Identify buttons, menu labels, and data labels
            if (IsUiKeyword(text)) analysis.Type = BlockType.UI;

            // 2. Status Bar: Identify numeric patterns like "1500/1500" (AP/XP)
            else if (Regex.IsMatch(text, @"\d+[\.,]?\d*\s*\/\s*\d+")) analysis.Type = BlockType.BarStatus;

            // 3. Governor ID: Digits 7-12, handling common OCR mistakes like 'ID' as 'ld'
            else if (Regex.IsMatch(text.Replace("l", "1").Replace("I", "1"), @"(ID|1D|ld)?\s*:?\s*\d{7,12}")) analysis.Type = BlockType.ID;

            // 4. Numbers: Pure numeric strings
            else if (IsNumber(text)) analysis.Type = BlockType.Number;

            // 5. Civilization: Match against game-specific civilizations
            else if (IsCivilization(text)) analysis.Type = BlockType.Civilization;

            // 6. Tags: Standard alliance tags in [TAG] format
            else if (text.StartsWith("[")) analysis.Type = BlockType.Tag;

            // 7. Metadata: Time (UTC) and date patterns
            else if (Regex.IsMatch(text, @"UTC|\d{1,2}:\d{2}")) analysis.Type = BlockType.DateOrTime;

            // 8. Default: Candidate for Name or Alliance Name
            else analysis.Type = BlockType.Unknown;

            list.Add(analysis);
        }
        return list;
    }

    private static bool IsUiKeyword(string text)
    {
        // Consolidate all label lists for broad filtering
        var allKeys = RokVocabulary.UiKeywords
            .Concat(RokVocabulary.StatusLabels)
            .Concat(RokVocabulary.GovernorLabels)
            .Concat(RokVocabulary.AllianceLabels)
            .Concat(RokVocabulary.PowerLabels);

        foreach (var key in allKeys)
        {
            // Direct comparison
            if (text.Contains(key, StringComparison.OrdinalIgnoreCase)) return true;
            
            // Fuzzy similarity check for low-confidence OCR reads (e.g., "Alianca" vs "Aliança")
            if (RokCognitiveTools.CalculateSimilarity(text, key) > 0.70) return true;
        }
        return false;
    }

    private static bool IsNumber(string text)
    {
        var clean = Regex.Replace(text, @"[^0-9]", "");
        return long.TryParse(clean, out _) && !text.Any(char.IsLetter);
    }

    private static bool IsCivilization(string text)
    {
        foreach (var civ in RokVocabulary.CleanCivilizations)
        {
            // Strong fuzzy match or direct inclusion check
            // Direct inclusion helps with OCR noise like "gChina" or "China-"
            if (RokCognitiveTools.CalculateSimilarity(text, civ) > 0.75 || 
                text.Contains(civ, StringComparison.OrdinalIgnoreCase)) 
                return true;
        }
        return false;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Cognitive;

public static class BlockClassifier
{
    public static List<AnalyzedBlock> Classify(List<OcrBlock> rawBlocks)
    {
        var list = new List<AnalyzedBlock>();
        foreach (var block in rawBlocks)
        {
            var analysis = new AnalyzedBlock { Raw = block };
            string text = block.Text.Trim();

            // 1. UI Keywords (Fuzzy matching)
            if (IsUiKeyword(text)) analysis.Type = BlockType.UI;

            // 2. Status Bar (e.g., 1500/1500)
            else if (Regex.IsMatch(text, @"\d+[\.,]?\d*\s*\/\s*\d+")) analysis.Type = BlockType.BarStatus;

            // 3. ID (8-12 digits) - Tolerant to L/I variations from OCR
            else if (Regex.IsMatch(text.Replace("l", "1").Replace("I", "1"), @"(ID|1D|ld)?\s*:?\s*\d{7,12}")) analysis.Type = BlockType.ID;

            // 4. Numbers
            else if (IsNumber(text)) analysis.Type = BlockType.Number;

            // 5. Civilization
            else if (IsCivilization(text)) analysis.Type = BlockType.Civilization;

            // 6. Tags [Tag]
            else if (text.StartsWith("[")) analysis.Type = BlockType.Tag;

            // 7. Date/Time
            else if (Regex.IsMatch(text, @"UTC|\d{1,2}:\d{2}")) analysis.Type = BlockType.DateOrTime;

            // 8. The Rest (Candidates for Name/Alliance)
            else analysis.Type = BlockType.Unknown;

            list.Add(analysis);
        }
        return list;
    }

    private static bool IsUiKeyword(string text)
    {
        var allKeys = RokVocabulary.UiKeywords
            .Concat(RokVocabulary.StatusLabels)
            .Concat(RokVocabulary.GovernorLabels)
            .Concat(RokVocabulary.AllianceLabels)
            .Concat(RokVocabulary.PowerLabels);

        foreach (var key in allKeys)
        {
            if (RokCognitiveTools.CalculateSimilarity(text, key) > 0.82) return true;
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
            if (RokCognitiveTools.CalculateSimilarity(text, civ) > 0.75) return true;
            // ADDITION: If it CONTAINS the civilization name, it is classified as Civ (catches OCR noise like gChina, SChina)
            if (text.Contains(civ, StringComparison.InvariantCultureIgnoreCase)) return true;
        }
        return false;
    }
}
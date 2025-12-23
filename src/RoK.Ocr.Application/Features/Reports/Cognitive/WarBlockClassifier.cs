using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Reports.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.Reports.Cognitive;

// Classe partial para suportar GeneratedRegex
public static partial class WarBlockClassifier
{
    // --- REGEX OTIMIZADOS ---
    [GeneratedRegex(@"^\[.+|\[.*\]", RegexOptions.Compiled)]
    private static partial Regex AllianceTagRegex();

    [GeneratedRegex(@"^\d+[KM]?$", RegexOptions.Compiled)]
    private static partial Regex PureNumberRegex();

    [GeneratedRegex(@"\d{2}:\d{2}|UTC|\d{2}/\d{2}", RegexOptions.Compiled)]
    private static partial Regex DateTimeRegex();

    [GeneratedRegex(@"X:?\s*\d+.*Y:?\s*\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CoordinateRegex();

    // Regex para limpar string numérica
    [GeneratedRegex(@"[^0-9]", RegexOptions.Compiled)]
    private static partial Regex NonDigitRegex();


    public static void ClassifyNodes(List<AnalyzedBlock> nodes)
    {
        foreach (var node in nodes)
        {
            string text = node.Raw.Text.Trim();

            // 1. TOP PRIORITY: Metric Labels
            if (MatchPartial(text, WarVocabulary.UnitsLabels)) node.Type = BlockType.UnitsLabel;
            else if (MatchPartial(text, WarVocabulary.DeadLabels)) node.Type = BlockType.DeadLabel;
            else if (MatchPartial(text, WarVocabulary.SevereWoundedLabels)) node.Type = BlockType.SevereWoundedLabel;
            else if (MatchPartial(text, WarVocabulary.SlightlyWoundedLabels)) node.Type = BlockType.SlightlyWoundedLabel;
            else if (MatchPartial(text, WarVocabulary.RemainingLabels)) node.Type = BlockType.RemainingLabel;
            else if (MatchPartial(text, WarVocabulary.HealedLabels)) node.Type = BlockType.HealedLabel;
            else if (MatchPartial(text, WarVocabulary.WatchtowerLabels)) node.Type = BlockType.WatchtowerLabel;
            else if (MatchPartial(text, WarVocabulary.KillPointsLabels)) node.Type = BlockType.KillPointsLabel;
            else if (MatchPartial(text, WarVocabulary.VictoryTerms) || MatchPartial(text, WarVocabulary.DefeatTerms))
                node.Type = BlockType.StatusResult;

            // 2. ALLIANCE TAGS (Usando Regex Otimizado)
            else if (AllianceTagRegex().IsMatch(text))
            {
                node.Type = BlockType.Tag;
            }

            // 3. PURE NUMBERS (Usando Regex Otimizado)
            else if (IsPureNumeric(text))
            {
                node.Type = BlockType.Number;
            }

            // 4. METADATA (Usando Regex Otimizado)
            else if (DateTimeRegex().IsMatch(text) || CoordinateRegex().IsMatch(text))
            {
                node.Type = BlockType.DateOrTime;
            }

            // 5. UI FILTER
            else if (WarVocabulary.GlobalBlacklist.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase)) ||
                     WarVocabulary.UiBlacklist.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase)))
            {
                node.Type = BlockType.UI;
            }

            // 6. UNKNOWN
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
            if (text.Contains(v, StringComparison.OrdinalIgnoreCase)) return true;
            
            // Otimização: Só faz split e fuzzy se não achou direto
            if (RokCognitiveTools.CalculateSimilarity(text, v) > 0.85) return true;
        }
        return false;
    }

    private static bool IsPureNumeric(string text)
    {
        // Limpeza leve manual antes do regex
        string clean = text.Replace(".", "").Replace(",", "").Replace(" ", "").Replace("+", "").Trim();
        return PureNumberRegex().IsMatch(clean);
    }
}
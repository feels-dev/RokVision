using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Reports.Constants;
using RoK.Ocr.Application.Shared.Cognitive;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;

namespace RoK.Ocr.Application.Reports.Neurons;

public class WarMetricNeuron
{
    // ADD the final parameter: double minY = 0.0
    public void PopulateSide(TopologyGraph graph, BattleSide side, SideLocation location, List<AnalyzedBlock> allNodes, double minY = 0.0)
    {
        double minX = location == SideLocation.Attacker ? 0.0 : 0.5;
        double maxX = location == SideLocation.Attacker ? 0.5 : 1.0;

        // CRITICAL FILTER: Now using minY to ignore summaries at the top of the print
        var sideNodes = graph.GetNodesInRegion(minX, maxX, minY, 1.0);

        ExtractValue(sideNodes, graph, BlockType.UnitsLabel, v => side.TotalUnits = v);
        ExtractValue(sideNodes, graph, BlockType.HealedLabel, v => side.Healed = v);
        ExtractValue(sideNodes, graph, BlockType.DeadLabel, v => side.Dead = v);
        ExtractValue(sideNodes, graph, BlockType.SevereWoundedLabel, v => side.SeverelyWounded = v);
        ExtractValue(sideNodes, graph, BlockType.SlightlyWoundedLabel, v => side.SlightlyWounded = v);
        ExtractValue(sideNodes, graph, BlockType.RemainingLabel, v => side.Remaining = v);
        ExtractValue(sideNodes, graph, BlockType.KillPointsLabel, v => side.KillPointsGained = v);
        ExtractValue(sideNodes, graph, BlockType.WatchtowerLabel, v => side.WatchtowerDamage = v);
    }

    private void ExtractValue(List<AnalyzedBlock> nodes, TopologyGraph graph, BlockType labelType, Action<long> setter)
    {
        var labelNode = nodes.FirstOrDefault(n => n.Type == labelType);
        if (labelNode == null) return;

        long val = ParseRokNumber(labelNode.Raw.Text);
        if (val <= 1)
        {
            var valueNode = graph.FindNeighbor(labelNode, Direction.Right, 0.45);
            if (valueNode != null) val = ParseRokNumber(valueNode.Raw.Text);
        }
        setter(val);
    }

    private long ParseRokNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains("%")) return 0;

        string clean = text.ToUpper();

        // 1. Dynamic Cleaning via Vocabulary
        var allLabels = WarVocabulary.UnitsLabels
            .Concat(WarVocabulary.DeadLabels)
            .Concat(WarVocabulary.SevereWoundedLabels)
            .Concat(WarVocabulary.SlightlyWoundedLabels)
            .Concat(WarVocabulary.RemainingLabels)
            .Concat(WarVocabulary.HealedLabels)
            .Concat(WarVocabulary.WatchtowerLabels)
            .Concat(WarVocabulary.KillPointsLabels);

        foreach (var label in allLabels)
        {
            clean = clean.Replace(label.ToUpper(), "");
        }

        // Cleaning of common noisy characters
        clean = clean.Replace("|", " ")
                     .Replace("I", "1")
                     .Replace("L", "1")
                     .Replace("O", "0")
                     .Replace("+", "")
                     .Trim();

        // 2. ARMORED REGEX: Captures the number (with dot/comma) and the suffix (K/M) separately
        // Group 1: (\d+[\.,]?\d*) -> The numeric value
        // Group 2: ([KM]?) -> The multiplier suffix
        var match = Regex.Match(clean, @"(\d+[\.,]?\d*)\s*([KM]?)");

        if (!match.Success) return 0;

        string numberPart = match.Groups[1].Value.Replace(",", "."); // Unifies decimal to dot
        string suffix = match.Groups[2].Value;

        // 3. THOUSAND TREATMENT (The problem of 1.200.000 or 40.342)
        // If there is more than one dot, it is a thousand separator (e.g., 1.200.000)
        if (numberPart.Count(f => f == '.') > 1)
        {
            numberPart = numberPart.Replace(".", "");
        }
        // If there is only one dot followed by exactly 3 digits at the end, 
        // and NO M/K suffix, it is a thousand (RoK standard: 40.342)
        else if (string.IsNullOrEmpty(suffix) && Regex.IsMatch(numberPart, @"\.\d{3}$"))
        {
            numberPart = numberPart.Replace(".", "");
        }

        // 4. FINAL PARSE AND MULTIPLICATION
        if (double.TryParse(numberPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
        {
            if (suffix == "K") val *= 1000;
            else if (suffix == "M") val *= 1000000;

            // Math.Round to avoid floating point inaccuracies during conversion to long
            return (long)Math.Round(val);
        }

        return 0;
    }
}
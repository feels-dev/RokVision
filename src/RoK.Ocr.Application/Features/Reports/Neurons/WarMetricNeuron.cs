using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Reports.Constants;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;

namespace RoK.Ocr.Application.Features.Reports.Neurons;

public partial class WarMetricNeuron
{
    [GeneratedRegex(@"(\d+[\.,]?\d*)\s*([KM]?)", RegexOptions.Compiled)]
    private static partial Regex NumberParserRegex();

    public void PopulateSide(TopologyGraph graph, BattleSide side, SideLocation location, List<AnalyzedBlock> allNodes, double minY, out List<AnalyzedBlock> usedMetricBlocks)
    {
        usedMetricBlocks = new List<AnalyzedBlock>();

        // FIX: Compare the 'location' parameter, not the 'side' object
        double minX = location == SideLocation.Attacker ? 0.0 : 0.5;
        double maxX = location == SideLocation.Attacker ? 0.5 : 1.0;
        var sideNodes = graph.GetNodesInRegion(minX, maxX, minY, 1.0);

        var (val_units, block_units) = ExtractValue(sideNodes, graph, BlockType.UnitsLabel);
        side.TotalUnits = val_units;
        if (block_units != null) usedMetricBlocks.Add(block_units);

        var (val_healed, block_healed) = ExtractValue(sideNodes, graph, BlockType.HealedLabel);
        side.Healed = val_healed;
        if (block_healed != null) usedMetricBlocks.Add(block_healed);

        var (val_dead, block_dead) = ExtractValue(sideNodes, graph, BlockType.DeadLabel);
        side.Dead = val_dead;
        if (block_dead != null) usedMetricBlocks.Add(block_dead);

        var (val_sev, block_sev) = ExtractValue(sideNodes, graph, BlockType.SevereWoundedLabel);
        side.SeverelyWounded = val_sev;
        if (block_sev != null) usedMetricBlocks.Add(block_sev);

        var (val_sli, block_sli) = ExtractValue(sideNodes, graph, BlockType.SlightlyWoundedLabel);
        side.SlightlyWounded = val_sli;
        if (block_sli != null) usedMetricBlocks.Add(block_sli);

        var (val_rem, block_rem) = ExtractValue(sideNodes, graph, BlockType.RemainingLabel);
        side.Remaining = val_rem;
        if (block_rem != null) usedMetricBlocks.Add(block_rem);

        var (val_kp, block_kp) = ExtractValue(sideNodes, graph, BlockType.KillPointsLabel);
        side.KillPointsGained = val_kp;
        if (block_kp != null) usedMetricBlocks.Add(block_kp);

        var (val_wt, block_wt) = ExtractValue(sideNodes, graph, BlockType.WatchtowerLabel);
        side.WatchtowerDamage = val_wt;
        if (block_wt != null) usedMetricBlocks.Add(block_wt);
    }

    private (long Value, AnalyzedBlock? SourceBlock) ExtractValue(List<AnalyzedBlock> nodes, TopologyGraph graph, BlockType labelType)
    {
        var labelNode = nodes.FirstOrDefault(n => n.Type == labelType);
        if (labelNode == null) return (0, null);

        long val = ParseRokNumber(labelNode.Raw.Text);
        if (val > 1)
        {
            return (val, labelNode);
        }

        var valueNode = graph.FindNeighbor(labelNode, Direction.Right, 0.45);
        if (valueNode != null)
        {
            val = ParseRokNumber(valueNode.Raw.Text);
            return (val, valueNode);
        }

        return (0, null);
    }

    private long ParseRokNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains("%")) return 0;
        string clean = text.ToUpper();

        var allLabels = WarVocabulary.UnitsLabels.Concat(WarVocabulary.DeadLabels).Concat(WarVocabulary.SevereWoundedLabels)
            .Concat(WarVocabulary.SlightlyWoundedLabels).Concat(WarVocabulary.RemainingLabels).Concat(WarVocabulary.HealedLabels)
            .Concat(WarVocabulary.WatchtowerLabels).Concat(WarVocabulary.KillPointsLabels);

        foreach (var label in allLabels) clean = clean.Replace(label.ToUpper(), "");

        clean = clean.Replace("|", " ").Replace("I", "1").Replace("L", "1").Replace("O", "0").Replace("+", "").Trim();

        var match = NumberParserRegex().Match(clean);
        if (!match.Success) return 0;

        string numberPart = match.Groups[1].Value.Replace(",", ".");
        string suffix = match.Groups[2].Value;

        if (numberPart.Count(f => f == '.') > 1) numberPart = numberPart.Replace(".", "");
        else if (string.IsNullOrEmpty(suffix) && Regex.IsMatch(numberPart, @"\.\d{3}$")) numberPart = numberPart.Replace(".", "");

        if (double.TryParse(numberPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
        {
            if (suffix == "K") val *= 1000;
            else if (suffix == "M") val *= 1000000;
            return (long)Math.Round(val);
        }
        return 0;
    }
}
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

// ATENÇÃO: A classe agora é 'partial' para permitir o Source Generator
public partial class WarMetricNeuron
{
    // Otimização .NET 9: Regex compilado em tempo de build
    // Captura números com decimais opcionais e sufixos K/M
    [GeneratedRegex(@"(\d+[\.,]?\d*)\s*([KM]?)", RegexOptions.Compiled)]
    private static partial Regex NumberParserRegex();

    public void PopulateSide(TopologyGraph graph, BattleSide side, SideLocation location, List<AnalyzedBlock> allNodes, double minY = 0.0)
    {
        double minX = location == SideLocation.Attacker ? 0.0 : 0.5;
        double maxX = location == SideLocation.Attacker ? 0.5 : 1.0;

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

        var allLabels = WarVocabulary.UnitsLabels
            .Concat(WarVocabulary.DeadLabels)
            .Concat(WarVocabulary.SevereWoundedLabels)
            .Concat(WarVocabulary.SlightlyWoundedLabels)
            .Concat(WarVocabulary.RemainingLabels)
            .Concat(WarVocabulary.HealedLabels)
            .Concat(WarVocabulary.WatchtowerLabels)
            .Concat(WarVocabulary.KillPointsLabels);

        foreach (var label in allLabels) clean = clean.Replace(label.ToUpper(), "");

        clean = clean.Replace("|", " ").Replace("I", "1").Replace("L", "1").Replace("O", "0").Replace("+", "").Trim();
        
        // USO OTIMIZADO: Chama o método gerado
        var match = NumberParserRegex().Match(clean);

        if (!match.Success) return 0;

        string numberPart = match.Groups[1].Value.Replace(",", ".");
        string suffix = match.Groups[2].Value;

        if (numberPart.Count(f => f == '.') > 1) numberPart = numberPart.Replace(".", "");
        else if (string.IsNullOrEmpty(suffix) && Regex.IsMatch(numberPart, @"\.\d{3}$")) numberPart = numberPart.Replace(".", "");

        if (double.TryParse(numberPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
        {
            if (suffix == "K") val *= 1000;
            else if (suffix == "M") val *= 1000000;
            return (long)Math.Round(val);
        }
        return 0;
    }
}
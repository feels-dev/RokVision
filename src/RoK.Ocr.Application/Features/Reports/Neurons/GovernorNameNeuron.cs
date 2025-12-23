using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Reports.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;
using FuzzySharp;

namespace RoK.Ocr.Application.Features.Reports.Neurons;

public class GovernorNameNeuron
{
    private readonly List<CommanderEntry> _commanders;

    public GovernorNameNeuron(List<CommanderEntry> commanders)
    {
        _commanders = commanders;
    }

    /// <summary>
    /// Extracts the governor's name prioritizing tag slicing or spatial neighborhood.
    /// </summary>
    public string Extract(TopologyGraph graph, AnalyzedBlock? tagNode, SideLocation side, List<AnalyzedBlock> allNodes, string suffix = "")
    {
        if (!string.IsNullOrWhiteSpace(suffix) && IsValidPlayerName(suffix))
            return CleanName(suffix);

        if (tagNode != null)
        {
            var neighbor = graph.FindNeighbor(tagNode, Direction.Right, 0.35);
            if (neighbor != null && IsValidPlayerName(neighbor.Raw.Text))
                return CleanName(neighbor.Raw.Text);
        }

        // --- SNIPER ADJUSTMENT: Restricts X to ignore the side list ---
        double minX = side == SideLocation.Attacker ? 0.22 : 0.55;
        double maxX = side == SideLocation.Attacker ? 0.48 : 0.88;

        var candidates = allNodes
            .Where(n => n.NormalizedCenter.X >= minX && n.NormalizedCenter.X <= maxX)
            .Where(n => n.NormalizedCenter.Y > 0.08 && n.NormalizedCenter.Y < 0.28) // Exact name zone
            .Where(n => n.Type == BlockType.Unknown)
            .Where(n => IsValidPlayerName(n.Raw.Text))
            .OrderBy(n => n.NormalizedCenter.Y)
            .ToList();

        return candidates.Any() ? CleanName(candidates.First().Raw.Text) : "--";
    }

    private bool IsValidPlayerName(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2) return false;

        // 1. DATE FILTER: If it matches date pattern (####/##/##), it's not a name
        if (Regex.IsMatch(text, @"\d{4}/\d{2}/\d{2}")) return false;

        // 2. TIME FILTER: If it matches time pattern (##:##), it's not a name
        if (Regex.IsMatch(text, @"\d{2}:\d{2}")) return false;

        // 3. COORDINATES FILTER: Already exists, but reinforcing
        if (Regex.IsMatch(text, @"X:?\s*\d+.*Y:?\s*\d+", RegexOptions.IgnoreCase)) return false;

        // 4. GAME TERMS FILTER: "Remaining", "Power", "Marauders" (in Portuguese context)
        var pveTerms = new[] { "Restante", "Poder", "Invasores", "Chefes", "Vago", "Mensagem" };
        if (pveTerms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase))) return false;

        // 5. GLOBAL BLACKLIST (C# must use WarVocabulary)
        if (WarVocabulary.UiBlacklist.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase))) return false;

        return true;
    }

    private string CleanName(string text)
    {
        // Cleans bracket residues but keeps international characters
        return Regex.Replace(text, @"[\[\]]", "").Trim();
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FuzzySharp;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;

namespace RoK.Ocr.Application.Features.Reports.Neurons;

public class CommanderNeuron
{
    private readonly List<CommanderEntry> _vocabulary;
    private readonly ILogger<CommanderNeuron> _logger;

    public CommanderNeuron(List<CommanderEntry> vocabulary, ILogger<CommanderNeuron>? logger = null)
    {
        _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
        _logger = logger!;
    }

    public List<CommanderEntry> Extract(TopologyGraph graph, SideLocation side, List<AnalyzedBlock> allNodes)
    {
        var foundEntries = new List<(CommanderEntry Entry, double Y)>();

        double minX = side == SideLocation.Attacker ? 0.0 : 0.5;
        double maxX = side == SideLocation.Attacker ? 0.5 : 1.0;

        // Typical zone where commanders appear
        var zoneNodes = graph.GetNodesInRegion(minX, maxX, 0.05, 0.70);

        // Anchors: blocks with "Nv.", "Lv.", "Nível"
        var anchors = zoneNodes
            .Where(n => n.Type != BlockType.UI)
            .Where(n => Regex.IsMatch(n.Raw.Text, @"[LN][vV]\.?\s*\d+", RegexOptions.IgnoreCase))
            .ToList();

        foreach (var anchor in anchors)
        {
            string rawName = ExtractRawName(graph, anchor, zoneNodes);
            if (string.IsNullOrWhiteSpace(rawName)) continue;

            var match = MatchBestCommander(rawName);
            if (match != null)
            {
                foundEntries.Add((match, anchor.NormalizedCenter.Y));
            }
        }

        return foundEntries
            .OrderBy(f => f.Y)
            .Select(f => f.Entry)
            .DistinctBy(e => e.Id)
            .Take(2)
            .ToList();
    }

    private CommanderEntry? MatchBestCommander(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || !_vocabulary.Any())
            return null;

        // Normalizes by removing accents and converting to lowercase
        string normalizedInput = NormalizeString(input);

        // Pre-normalizes all labels once (performance gain)
        var normalizedLabels = _vocabulary
            .SelectMany(v => v.Labels.Select(label => new { Label = NormalizeString(label), Entry = v }))
            .ToList();

        // Gets the top 3 candidates
        var candidates = Process.ExtractTop(normalizedInput, normalizedLabels.Select(n => n.Label), limit: 3);

        _logger?.LogDebug("[CommanderNeuron] Input: '{Input}' → Normalized: '{Normalized}' | Top candidates: {Candidates}",
            input,
            normalizedInput,
            string.Join(" | ", candidates.Select(c => $"{c.Value} ({c.Score})")));

        foreach (var candidate in candidates)
        {
            if (candidate.Score >= 83) // Threshold adjusted (was 65 -> now 83)
            {
                var match = normalizedLabels.FirstOrDefault(n => n.Label == candidate.Value)?.Entry;
                if (match != null)
                {
                    _logger?.LogInformation("[CommanderNeuron] MATCH CONFIRMED: '{Input}' → {CanonicalName} (Score: {Score})",
                        input, match.CanonicalName, candidate.Score);
                    return match;
                }
            }
        }

        _logger?.LogDebug("[CommanderNeuron] NO MATCH for '{Input}' (best score below threshold)", input);
        return null;
    }

    private string ExtractRawName(TopologyGraph graph, AnalyzedBlock anchor, List<AnalyzedBlock> localNodes)
    {
        string text = anchor.Raw.Text;

        // Removes "Nv. 23", "Lv. 30", "Nível 40", etc.
        string clean = Regex.Replace(text, @"([LN][vV]\.?|Nível)\s*\d+", "", RegexOptions.IgnoreCase).Trim();

        if (string.IsNullOrEmpty(clean))
        {
            var neighbor = graph.FindNeighbor(anchor, Direction.Right, 0.35);
            if (neighbor != null && neighbor.Type == BlockType.Unknown)
                clean = neighbor.Raw.Text;
        }

        // Removes loose numbers that might remain (e.g., "23 Sun Tzu" -> "Sun Tzu")
        clean = Regex.Replace(clean, @"^\d+\s*", "").Trim();

        return clean;
    }

    /// <summary>
    /// Removes accents and diacritics (Péricles -> Pericles, João -> Joao, Ç -> C)
    /// </summary>
    private static string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        string decomposed = input.Normalize(NormalizationForm.FormD);
        var filtered = decomposed
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();

        return new string(filtered).ToLowerInvariant();
    }
}
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
    public List<ExtractionResult<CommanderEntry>> Extract(TopologyGraph graph, SideLocation side, List<AnalyzedBlock> allNodes)
    {
        var foundEntries = new List<(ExtractionResult<CommanderEntry> Result, double Y)>();

        double minX = side == SideLocation.Attacker ? 0.0 : 0.5;
        double maxX = side == SideLocation.Attacker ? 0.5 : 1.0;

        var zoneNodes = graph.GetNodesInRegion(minX, maxX, 0.05, 0.70);

        var anchors = zoneNodes
            .Where(n => n.Type != BlockType.UI)
            .Where(n => Regex.IsMatch(n.Raw.Text, @"[LN][vV]\.?\s*\d+", RegexOptions.IgnoreCase))
            .ToList();

        foreach (var anchor in anchors)
        {
            var (rawName, nameBlock) = ExtractRawName(graph, anchor, zoneNodes);
            if (string.IsNullOrWhiteSpace(rawName)) continue;

            var matchResult = MatchBestCommander(rawName);
            if (matchResult.Match != null)
            {
                var extraction = new ExtractionResult<CommanderEntry>
                {
                    Value = matchResult.Match,
                    Confidence = matchResult.Score,
                    SourceBlock = nameBlock ?? anchor,
                    Strategy = "Commander_FuzzyMatch"
                };
                foundEntries.Add((extraction, anchor.NormalizedCenter.Y));
            }
        }

        return foundEntries
            .OrderBy(f => f.Y)
            .Select(f => f.Result)
            .DistinctBy(e => e.Value.Id)
            .Take(2)
            .ToList();
    }

    private (CommanderEntry? Match, int Score) MatchBestCommander(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || !_vocabulary.Any()) return (null, 0);

        string normalizedInput = NormalizeString(input);
        var normalizedLabels = _vocabulary
            .SelectMany(v => v.Labels.Select(label => new { Label = NormalizeString(label), Entry = v }))
            .ToList();

        var candidates = Process.ExtractTop(normalizedInput, normalizedLabels.Select(n => n.Label), limit: 3);

        foreach (var candidate in candidates)
        {
            if (candidate.Score >= 83)
            {
                var match = normalizedLabels.FirstOrDefault(n => n.Label == candidate.Value)?.Entry;
                if (match != null) return (match, candidate.Score);
            }
        }
        return (null, 0);
    }

    private (string Name, AnalyzedBlock? Block) ExtractRawName(TopologyGraph graph, AnalyzedBlock anchor, List<AnalyzedBlock> localNodes)
    {
        string text = anchor.Raw.Text;
        string clean = Regex.Replace(text, @"([LN][vV]\.?|Nível)\s*\d+", "", RegexOptions.IgnoreCase).Trim();

        if (string.IsNullOrEmpty(clean))
        {
            var neighbor = graph.FindNeighbor(anchor, Direction.Right, 0.35);
            if (neighbor != null && neighbor.Type == BlockType.Unknown)
                return (Regex.Replace(neighbor.Raw.Text, @"^\d+\s*", "").Trim(), neighbor);
        }

        return (Regex.Replace(clean, @"^\d+\s*", "").Trim(), anchor);
    }

    private static string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        string decomposed = input.Normalize(NormalizationForm.FormD);
        var filtered = decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        return new string(filtered).ToLowerInvariant();
    }
}
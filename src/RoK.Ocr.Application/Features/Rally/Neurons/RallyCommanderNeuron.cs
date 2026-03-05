using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp; 
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports; 

namespace RoK.Ocr.Application.Features.Rally.Neurons;

public class RallyCommanderNeuron
{
    private readonly List<CommanderEntry> _vocabulary;

    public RallyCommanderNeuron(List<CommanderEntry> vocabulary)
    {
        _vocabulary = vocabulary;
    }

    public (CommanderEntry? Primary, CommanderEntry? Secondary) Extract(List<AnalyzedBlock> rowNodes)
    {
        var found = new List<CommanderEntry>();

        var candidates = rowNodes
            .Where(n => n.Raw.Text.Length > 3)
            // Filters out status keywords that trigger false positives (e.g., "Gorgo" misidentified as "Chegou"/"Arrived")
            .Where(n => !RallyVocabulary.ArrivedLabels.Any(l => Fuzz.PartialRatio(n.Raw.Text.ToLower(), l.ToLower()) > 80))
            .Where(n => !n.Raw.Text.Contains("Unidades", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.NormalizedCenter.X) 
            .ToList();

        foreach (var node in candidates)
        {
            // Intelligent string splitting for dual-commander entries (e.g., Lohar / Aethelflaed)
            var parts = node.Raw.Text.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                string text = CleanText(part);
                if (string.IsNullOrWhiteSpace(text) || text.Length < 3) continue;

                var match = Process.ExtractOne(text, _vocabulary.SelectMany(c => c.Labels), cutoff: 80);

                if (match != null)
                {
                    var entry = _vocabulary.FirstOrDefault(c => c.Labels.Contains(match.Value));
                    if (entry != null && !found.Contains(entry))
                    {
                        found.Add(entry);
                    }
                }
            }
        }

        return (
            found.FirstOrDefault(), 
            found.Skip(1).FirstOrDefault()
        );
    }

    private string CleanText(string text)
    {
        return Regex.Replace(text, @"(Nv\.|Lvl|Level)\s*\d+", "", RegexOptions.IgnoreCase).Trim();
    }
}
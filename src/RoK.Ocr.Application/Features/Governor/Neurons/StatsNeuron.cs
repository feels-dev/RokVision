using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Common.Interfaces;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.Governor.Neurons;

public class StatsNeuron : IOcrNeuron<long>
{
    private readonly bool _requireBigNumber;
    private readonly long _excludeValue;

    public StatsNeuron(bool requireBigNumber = true, long excludeValue = -1)
    {
        _requireBigNumber = requireBigNumber;
        _excludeValue = excludeValue;
    }

    // Note: The Orchestrator must pass the correct anchor (PowerLabel or KpLabel) in the dictionary as "TargetLabel"
    public ExtractionResult<long> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        if (!anchors.ContainsKey("TargetLabel")) 
            return new ExtractionResult<long> { Value = 0, Confidence = 0 };

        var label = anchors["TargetLabel"];

        // Candidates: Numbers
        var candidates = allBlocks
            .Except(blacklist)
            .Where(b => b.Type == BlockType.Number)
            .Select(b => new { Val = ParseLong(b.Raw.Text), Block = b })
            .Where(x => x.Val != _excludeValue)
            .ToList();

        if (_requireBigNumber)
        {
            candidates = candidates.Where(x => x.Val > 1000).ToList();
        }

        if (!candidates.Any()) 
            return new ExtractionResult<long> { Value = 0, Confidence = 0 };

        // Picks the closest one
        var winner = candidates
            .OrderBy(x => RokCognitiveTools.CalculateDistance(x.Block.Center.X, x.Block.Center.Y, label.Center.X, label.Center.Y))
            .First();

        return new ExtractionResult<long>
        {
            Value = winner.Val,
            Confidence = 85,
            SourceBlock = winner.Block
        };
    }

    private long ParseLong(string text)
    {
        var clean = text.Replace(".", "").Replace(",", "").Replace("K", "000").Replace("M", "000000");
        var match = Regex.Match(clean, @"\d+");
        if (match.Success && long.TryParse(match.Value, out long val)) return val;
        return 0;
    }
}
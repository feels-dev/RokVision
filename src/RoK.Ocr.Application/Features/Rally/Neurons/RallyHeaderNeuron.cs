using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Rally;

namespace RoK.Ocr.Application.Features.Rally.Neurons;

public partial class RallyHeaderNeuron
{
    [GeneratedRegex(@"\[(?<tag>.*?)\]\s*(?<name>.*)", RegexOptions.Compiled)]
    private static partial Regex TagAndNameRegex();

    [GeneratedRegex(@"X[:\s]*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex XCoordRegex();

    [GeneratedRegex(@"Y[:\s]*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex YCoordRegex();

    [GeneratedRegex(@"(\d[\d\.,]*)\s*/\s*(\d[\d\.,]*)", RegexOptions.Compiled)]
    private static partial Regex CapacityRegex();[GeneratedRegex(@"\d{2}:\d{2}:\d{2}", RegexOptions.Compiled)]
    private static partial Regex TimerRegex();[GeneratedRegex(@"[^\d]", RegexOptions.Compiled)]
    private static partial Regex NonDigitRegex();

    public void Extract(List<AnalyzedBlock> analyzedBlocks, RallyResult result, HashSet<AnalyzedBlock> usedBlocks, double bottomBoundaryY, int imgW, int imgH)
    {
        // Retrieves all text blocks located above the defined bottom boundary (Y-axis), 
        // regardless of horizontal alignment constraints.
        var headerNodes = analyzedBlocks
            .Where(n => n.Raw.Box[0][1] / (double)imgH < bottomBoundaryY)
            .Except(usedBlocks)
            .ToList();

        // 1. Leader Tag and Name extraction (Pattern: [TAG] Name)
        var tagBlock = headerNodes.FirstOrDefault(n => TagAndNameRegex().IsMatch(n.Raw.Text));
        if (tagBlock != null)
        {
            var parts = ParseTagAndName(tagBlock.Raw.Text);
            result.Leader.AllianceTag = parts.Tag;
            result.Leader.Name = parts.Name;
            usedBlocks.Add(tagBlock);
        }

        // 2. Leader Coordinates (Selects the leftmost coordinate block in the header)
        var coordBlock = headerNodes
            .Where(n => XCoordRegex().IsMatch(n.Raw.Text) || YCoordRegex().IsMatch(n.Raw.Text))
            .OrderBy(n => n.Raw.Box[0][0])
            .FirstOrDefault();

        if (coordBlock != null)
        {
            var (x, y) = ParseCoordinates(coordBlock.Raw.Text);
            result.Leader.X = x;
            result.Leader.Y = y;
            usedBlocks.Add(coordBlock);
        }

        // 3. Rally Capacity extraction (Pattern: X / Y)
        var capacityBlock = headerNodes.FirstOrDefault(n =>
            CapacityRegex().IsMatch(n.Raw.Text) &&
            !n.Raw.Text.Contains("UTC", StringComparison.OrdinalIgnoreCase) && // Ignores date/time blocks containing "UTC"
            n.Raw.Box[0][0] / (double)imgW > 0.15 // Prevents accidental capture of the left-aligned clock interface
        );
        if (capacityBlock != null)
        {
            var caps = ParseCapacity(capacityBlock.Raw.Text);
            result.Status.CurrentCapacity = caps.Current;
            result.Status.MaxCapacity = caps.Max;
            usedBlocks.Add(capacityBlock);
        }

        // 4. Timer extraction
        var timerBlock = headerNodes.FirstOrDefault(n => TimerRegex().IsMatch(n.Raw.Text));
        if (timerBlock != null)
        {
            result.Status.TimeRemaining = TimerRegex().Match(timerBlock.Raw.Text).Value;
            usedBlocks.Add(timerBlock);
        }

        // 5. Rally State extraction (Preparing / Marching)
        var stateBlock = headerNodes.FirstOrDefault(n =>
            RallyVocabulary.PreparingLabels.Any(l => Fuzz.PartialRatio(n.Raw.Text.ToLower(), l.ToLower()) > 80) ||
            RallyVocabulary.MarchingLabels.Any(l => Fuzz.PartialRatio(n.Raw.Text.ToLower(), l.ToLower()) > 80)
        );

        if (stateBlock != null)
        {
            bool isPreparing = RallyVocabulary.PreparingLabels.Any(l => Fuzz.PartialRatio(stateBlock.Raw.Text.ToLower(), l.ToLower()) > 80);
            result.Status.State = isPreparing ? RallyVocabulary.StatePreparing : RallyVocabulary.StateMarching;
            usedBlocks.Add(stateBlock);
        }
    }

    private (string Tag, string Name) ParseTagAndName(string text)
    {
        var match = TagAndNameRegex().Match(text);
        if (match.Success) return (match.Groups["tag"].Value.Trim(), match.Groups["name"].Value.Trim());
        return ("--", text.Trim());
    }

    private (int X, int Y) ParseCoordinates(string text)
    {
        int x = 0, y = 0;
        var xMatch = XCoordRegex().Match(text);
        var yMatch = YCoordRegex().Match(text);
        if (xMatch.Success) int.TryParse(xMatch.Groups[1].Value, out x);
        if (yMatch.Success) int.TryParse(yMatch.Groups[1].Value, out y);
        return (x, y);
    }

    private (long Current, long Max) ParseCapacity(string text)
    {
        var match = CapacityRegex().Match(text);
        if (match.Success) return (CleanNumber(match.Groups[1].Value), CleanNumber(match.Groups[2].Value));
        return (0, 0);
    }

    private long CleanNumber(string val) => long.TryParse(NonDigitRegex().Replace(val, ""), out long result) ? result : 0;
}
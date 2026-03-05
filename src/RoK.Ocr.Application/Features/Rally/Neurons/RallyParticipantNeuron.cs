using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Rally;

namespace RoK.Ocr.Application.Features.Rally.Neurons;

public partial class RallyParticipantNeuron
{
    private readonly RallyCommanderNeuron _commanderNeuron;[GeneratedRegex(@"(Nv\.|Lvl|Level)\s*\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CommanderLevelRegex();[GeneratedRegex(@"[^\p{L}\p{N}\s\[\]=]", RegexOptions.Compiled)]
    private static partial Regex NameCleanupRegex();

    [GeneratedRegex(@"[^\d]", RegexOptions.Compiled)]
    private static partial Regex NonDigitRegex();[GeneratedRegex(@"\d{2}:\d{2}:\d{2}|\d{2}:\d{2}", RegexOptions.Compiled)]
    private static partial Regex TimerRegex();

    public RallyParticipantNeuron(IVocabularyLoader vocabLoader)
    {
        _commanderNeuron = new RallyCommanderNeuron(vocabLoader.GetCommanders());
    }

    public (List<RallyParticipant>, List<(AnalyzedBlock Node, string CommanderName)>) ExtractParticipants(
        TopologyGraph graph, 
        HashSet<AnalyzedBlock> usedBlocks, 
        double listTopBoundaryY)
    {
        var participants = new List<RallyParticipant>();
        var anchorData = new List<(AnalyzedBlock, string)>();

        // 1. Isolates the participant list area (Strictly below the global troop summary boundary)
        var listNodes = graph.GetNodesInRegion(0.0, 1.0, listTopBoundaryY, 1.0).Except(usedBlocks).ToList();

        // 2. Identifies anchor points. The optimal anchor is the Commander Level row (e.g., "Lvl 47 Lohar")
        var anchorNodes = listNodes
            .Where(n => CommanderLevelRegex().IsMatch(n.Raw.Text))
            .OrderBy(n => n.NormalizedCenter.Y)
            .ToList();

        // 3. Dynamic Relative Card Slicing
        for (int i = 0; i < anchorNodes.Count; i++)
        {
            var anchor = anchorNodes[i];
            
            // Computes relative Y-axis distances between adjacent player entries
            double prevAnchorY = i > 0 ? anchorNodes[i - 1].NormalizedCenter.Y : anchor.NormalizedCenter.Y - 0.12;
            double nextAnchorY = i < anchorNodes.Count - 1 ? anchorNodes[i + 1].NormalizedCenter.Y : anchor.NormalizedCenter.Y + 0.12;

            // Top-boundary extrapolation for the first list element
            if (i == 0 && anchorNodes.Count > 1) 
                prevAnchorY = anchor.NormalizedCenter.Y - (anchorNodes[1].NormalizedCenter.Y - anchor.NormalizedCenter.Y);
            
            // Bottom-boundary extrapolation for the last list element
            if (i == anchorNodes.Count - 1 && anchorNodes.Count > 1) 
                nextAnchorY = anchor.NormalizedCenter.Y + (anchor.NormalizedCenter.Y - anchorNodes[i - 1].NormalizedCenter.Y);

            // Defines the strict card boundary as the midpoint between consecutive anchors
            double cardMinY = (prevAnchorY + anchor.NormalizedCenter.Y) / 2.0;
            double cardMaxY = (anchor.NormalizedCenter.Y + nextAnchorY) / 2.0;

            // Isolates text blocks strictly within the calculated bounding card
            var cardBlocks = listNodes
                .Where(n => n.NormalizedCenter.Y >= cardMinY && n.NormalizedCenter.Y < cardMaxY)
                .ToList();

            if (!cardBlocks.Any()) continue;

            var participant = ProcessParticipantCard(cardBlocks, anchor);

            participants.Add(participant);
            anchorData.Add((anchor, participant.PrimaryCommander?.CanonicalName ?? ""));

            foreach (var b in cardBlocks) usedBlocks.Add(b);
        }

        return (participants, anchorData);
    }

    private RallyParticipant ProcessParticipantCard(List<AnalyzedBlock> cardBlocks, AnalyzedBlock anchor)
    {
        var participant = new RallyParticipant();

        var (primary, secondary) = _commanderNeuron.Extract(cardBlocks);
        participant.PrimaryCommander = primary;
        participant.SecondaryCommander = secondary;

        // Computes the horizontal center to distinguish left-aligned data (Name/Commander) 
        // from right-aligned data (Troops/Status)
        double minX = cardBlocks.Min(b => b.NormalizedCenter.X);
        double maxX = cardBlocks.Max(b => b.NormalizedCenter.X);
        double centerX = (minX + maxX) / 2.0;

        // Name heuristics: Left-aligned, located directly above the anchor, non-numeric
        var nameCandidate = cardBlocks
            .Where(n => n.NormalizedCenter.X < centerX) 
            .Where(n => n.NormalizedCenter.Y < anchor.NormalizedCenter.Y)
            .Where(n => !IsUnitLabel(n.Raw.Text) && !CommanderLevelRegex().IsMatch(n.Raw.Text))
            .Where(n => !Regex.IsMatch(n.Raw.Text, @"^\d+$"))
            .OrderByDescending(n => n.NormalizedCenter.Y) // Selects the closest text block directly above the anchor
            .FirstOrDefault();

        if (nameCandidate != null) participant.Name = CleanName(nameCandidate.Raw.Text);

        // Extracts troops count based on "Units:" label or highest numeric value on the right side
        var unitBlock = cardBlocks.FirstOrDefault(n => IsUnitLabel(n.Raw.Text));
        
        if (unitBlock != null)
        {
            participant.TotalUnits = ParseNumber(unitBlock.Raw.Text);
        }
        else
        {
            var numberCandidate = cardBlocks
                .Where(n => n.NormalizedCenter.X > centerX)
                .Where(n => Regex.IsMatch(n.Raw.Text, @"\d"))
                .Where(n => !TimerRegex().IsMatch(n.Raw.Text))
                .OrderByDescending(n => ParseNumber(n.Raw.Text))
                .FirstOrDefault();

            if (numberCandidate != null) participant.TotalUnits = ParseNumber(numberCandidate.Raw.Text);
        }

        // March Status heuristic (Matches specific keywords like "Arrived" or a timer pattern)
        var statusCandidate = cardBlocks.FirstOrDefault(n => IsStatusLabel(n.Raw.Text) || TimerRegex().IsMatch(n.Raw.Text));
        if (statusCandidate != null)
        {
            if (TimerRegex().IsMatch(statusCandidate.Raw.Text)) 
            {
                participant.MarchStatus = RallyVocabulary.StateMarching; 
            } 
            else 
            {
                bool isArrived = RallyVocabulary.ArrivedLabels.Any(l => Fuzz.PartialRatio(statusCandidate.Raw.Text.ToLower(), l.ToLower()) > 80);
                participant.MarchStatus = isArrived ? RallyVocabulary.StateArrived : RallyVocabulary.StateMarching;
            }
        }

        return participant;
    }

    private bool IsUnitLabel(string text) => RallyVocabulary.UnitsLabels.Any(l => Fuzz.PartialRatio(text.ToLower(), l.ToLower()) > 80) || text.ToLower().Contains("unidades");
    private bool IsStatusLabel(string text) => RallyVocabulary.ArrivedLabels.Any(l => Fuzz.PartialRatio(text.ToLower(), l.ToLower()) > 80) || RallyVocabulary.MarchingLabels.Any(l => Fuzz.PartialRatio(text.ToLower(), l.ToLower()) > 80);
    private long ParseNumber(string text) => long.TryParse(NonDigitRegex().Replace(text, ""), out long val) ? val : 0;
    
    private string CleanName(string text)
    {
        string cleaned = NameCleanupRegex().Replace(text, "").Trim();
        return cleaned.Length == 0 ? "--" : cleaned;
    }
}
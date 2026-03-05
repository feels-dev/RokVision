using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Rally;

namespace RoK.Ocr.Application.Features.Rally.Neurons;

public partial class RallyTargetNeuron
{
    private readonly List<NpcEntry> _knownNpcs;

    [GeneratedRegex(@"X[:\s]*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex XCoordRegex();[GeneratedRegex(@"Y[:\s]*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex YCoordRegex();[GeneratedRegex(@"(Nv\.|Lvl|Level)\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();[GeneratedRegex(@"^[\d\.,KMB]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PureNumbersRegex();

    [GeneratedRegex(@"\[(?<tag>.*?)\]", RegexOptions.Compiled)]
    private static partial Regex AllianceTagRegex();

    public RallyTargetNeuron(List<NpcEntry> knownNpcs)
    {
        _knownNpcs = knownNpcs;
    }

    public void Extract(List<AnalyzedBlock> analyzedBlocks, RallyResult result, HashSet<AnalyzedBlock> usedBlocks, double bottomBoundaryY, int imgW, int imgH)
    {
        // Isolates the upper right quadrant (Header target portrait area)
        var headerNodes = analyzedBlocks
            .Where(n => n.Raw.Box[0][0] / (double)imgW > 0.55 && 
                        n.Raw.Box[0][1] / (double)imgH > 0.05 && 
                        n.Raw.Box[0][1] / (double)imgH < bottomBoundaryY)
            .Except(usedBlocks)
            .ToList();

        if (!headerNodes.Any()) return;

        // 1. Target Coordinates Extraction
        var coordBlock = headerNodes.FirstOrDefault(n => n.Raw.Text.Contains("X:") || n.Raw.Text.Contains("Y:"));
        if (coordBlock != null)
        {
            var xMatch = XCoordRegex().Match(coordBlock.Raw.Text);
            var yMatch = YCoordRegex().Match(coordBlock.Raw.Text);

            if (xMatch.Success && int.TryParse(xMatch.Groups[1].Value, out int x)) result.Target.X = x;
            if (yMatch.Success && int.TryParse(yMatch.Groups[1].Value, out int y)) result.Target.Y = y;
            usedBlocks.Add(coordBlock);
        }

        // 2. Target Name Extraction
        // Ignores UI buttons (Cancel/Join) and isolated numeric blocks
        var targetNode = headerNodes
            .Where(n => n != coordBlock)
            .Where(n => !PureNumbersRegex().IsMatch(n.Raw.Text))
            .Where(n => !IsUiButton(n.Raw.Text))
            .OrderBy(n => n.Raw.Box[0][1]) // The target name is typically the topmost text block on the right side
            .FirstOrDefault();

        if (targetNode != null)
        {
            string rawText = targetNode.Raw.Text;
            usedBlocks.Add(targetNode);

            // CATEGORICAL DEDUCTION PIPELINE
            
            // Rule 1: Alliance Tag presence [TAG]. 
            // NPCs never have alliance tags, firmly indicating a PvP target (Flag, Fortress, City).
            if (AllianceTagRegex().IsMatch(rawText))
            {
                result.Target.IsNpc = false;
                result.Target.Name = CleanTargetName(rawText);
                return;
            }

            // Rule 2: Level prefix ("Lvl" / "Nv."). 
            // Players and alliance structures do not display level prefixes in the header; only NPCs do.
            var lvlMatch = LevelRegex().Match(rawText);
            if (lvlMatch.Success)
            {
                result.Target.IsNpc = true;
                if (int.TryParse(lvlMatch.Groups[2].Value, out int level)) result.Target.Level = level;
                
                string cleanedName = LevelRegex().Replace(rawText, "").Trim();
                result.Target.Name = EvaluateNpcName(cleanedName);
                return;
            }

            // Rule 3: Fuzzy matching against static in-game structures (Barbarian Forts, Holy Sites).
            var cleanRaw = CleanTargetName(rawText);
            var fortMatch = Process.ExtractOne(cleanRaw, RallyVocabulary.FortKeywords, cutoff: 80);
            var barbMatch = Process.ExtractOne(cleanRaw, RallyVocabulary.BarbarianKeywords, cutoff: 80);
            var structureMatch = Process.ExtractOne(cleanRaw, RallyVocabulary.StructureKeywords, cutoff: 80);

            if (fortMatch != null || barbMatch != null || structureMatch != null)
            {
                result.Target.IsNpc = true;
                if (fortMatch != null) result.Target.Name = RallyVocabulary.TargetFort;
                else if (barbMatch != null) result.Target.Name = RallyVocabulary.TargetBarbarian;
                else result.Target.Name = structureMatch!.Value;
                return;
            }

            // Fallback: If no NPC rules apply, it is classified as a PvP target (a player city being rallied).
            result.Target.IsNpc = false;
            result.Target.Name = cleanRaw.Length >= 2 ? cleanRaw : "Unknown Target";
        }
    }

    private string EvaluateNpcName(string cleanName)
    {
        var fortMatch = Process.ExtractOne(cleanName, RallyVocabulary.FortKeywords, cutoff: 75);
        if (fortMatch != null) return RallyVocabulary.TargetFort;

        var npcMatch = Process.ExtractOne(cleanName, _knownNpcs.SelectMany(n => n.Labels), cutoff: 80);
        if (npcMatch != null) return _knownNpcs.FirstOrDefault(n => n.Labels.Contains(npcMatch.Value))?.CanonicalName ?? cleanName;

        return CleanTargetName(cleanName);
    }

    private bool IsUiButton(string text) => 
        RallyVocabulary.UiButtons.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase)) || 
        text.Trim().Equals("X", StringComparison.OrdinalIgnoreCase) || 
        text.Contains("Entrar", StringComparison.OrdinalIgnoreCase);

    private string CleanTargetName(string text) => Regex.Replace(text, @"[^\p{L}\p{N}\s\[\]=]", "").Trim();
}
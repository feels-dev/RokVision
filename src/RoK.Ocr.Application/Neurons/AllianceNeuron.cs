using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Cognitive;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Neurons;

public class AllianceNeuron : IOcrNeuron<(string Tag, string Name)>
{
    public ExtractionResult<(string Tag, string Name)> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        // 1. DIRECT ATTEMPT: Block classified as TAG (Starts with [)
        var tagBlock = allBlocks
            .Except(blacklist)
            .FirstOrDefault(b => b.Type == BlockType.Tag);
        
        if (tagBlock != null)
        {
            var parsed = ParseAllianceString(tagBlock.Raw.Text);
            
            // If the name came back empty, get the right neighbor
            if (string.IsNullOrEmpty(parsed.Name))
            {
                var neighbor = FindClosestRight(tagBlock, allBlocks.Where(b => b.Type == BlockType.Unknown).ToList());
                if (neighbor != null) parsed.Name = CleanName(neighbor.Raw.Text);
            }

            // Accept if the tag is valid (>= 2 chars)
            if (parsed.Tag.Length >= 2 && parsed.Tag != "--")
            {
                return CreateResult(parsed, 95, tagBlock);
            }
        }

        // 2. ATTEMPT VIA "ALLIANCE" LABEL
        if (anchors.ContainsKey("AllianceLabel"))
        {
            var label = anchors["AllianceLabel"];
            
            var candidates = allBlocks
                .Except(blacklist)
                .Where(b => b.Type == BlockType.Unknown)
                .OrderBy(b => RokCognitiveTools.CalculateDistance(b.Center.X, b.Center.Y, label.Center.X, label.Center.Y))
                .Take(3);

            foreach (var candidate in candidates)
            {
                if (IsUiKeyword(candidate.Raw.Text)) continue;

                var result = ParseAllianceString(candidate.Raw.Text);
                
                // If a valid tag was found
                if (result.Tag.Length >= 2 && result.Tag != "--") 
                {
                    return CreateResult(result, 80, candidate);
                }
            }
        }

        return CreateResult(("--", "--"), 0, null);
    }

    private (string Tag, string Name) ParseAllianceString(string text)
    {
        text = text.Trim();
        
        // CASE A: Has closing bracket ]
        int closeIndex = text.IndexOf(']');
        if (closeIndex > 0)
        {
            var rawTagContent = text.Substring(0, closeIndex).Replace("[", "").Trim();
            string finalTag = SmartCleanTag(rawTagContent);

            string name = "";
            if (closeIndex + 1 < text.Length)
            {
                name = CleanName(text.Substring(closeIndex + 1));
            }

            return (finalTag, name);
        }

        // CASE B: Starts with [ without closing bracket
        if (text.StartsWith("["))
        {
            string cleanText = text.Substring(1);
            int spaceIndex = cleanText.IndexOf(' ');
            
            string rawTagPart;
            string namePart;

            if (spaceIndex > 0 && spaceIndex <= 5)
            {
                rawTagPart = cleanText.Substring(0, spaceIndex);
                namePart = cleanText.Substring(spaceIndex + 1);
            }
            else
            {
                int cut = cleanText.Length >= 4 ? 4 : cleanText.Length;
                rawTagPart = cleanText.Substring(0, cut);
                namePart = cleanText.Length > cut ? cleanText.Substring(cut) : "";
            }

            return (SmartCleanTag(rawTagPart), CleanName(namePart));
        }

        // CASE C: Loose text
        var parts = text.Split(' ', 2);
        if (parts.Length > 1)
        {
            string candidateTag = SmartCleanTag(parts[0]);
            if (candidateTag.Length >= 2 && candidateTag.Length <= 5)
            {
                return (candidateTag, CleanName(parts[1]));
            }
        }

        return ("--", CleanName(text));
    }

    private string SmartCleanTag(string rawInput)
    {
        string clean = Regex.Replace(rawInput, @"[^a-zA-Z0-9\-_.""':!@#$%&*+=<>?]", "");
        return clean.Trim();
    }

    private ExtractionResult<(string, string)> CreateResult((string, string) val, double conf, AnalyzedBlock? block)
    {
        return new ExtractionResult<(string, string)> { Value = val, Confidence = conf, SourceBlock = block };
    }

    private string CleanName(string text) => Regex.Replace(text, @"[^\w\s\-\[\]\u4e00-\u9fa5]", "").Trim();

    private bool IsUiKeyword(string text)
    {
        return text.Contains("Alianca", StringComparison.InvariantCultureIgnoreCase) ||
            text.Contains("Alliance", StringComparison.InvariantCultureIgnoreCase);
    }

    private AnalyzedBlock? FindClosestRight(AnalyzedBlock target, List<AnalyzedBlock> candidates)
    {
        return candidates
            .Where(c => c.Center.X > target.Center.X)
            .Where(c => Math.Abs(c.Center.Y - target.Center.Y) < 40)
            .OrderBy(c => Math.Abs(c.Center.X - target.Center.X))
            .FirstOrDefault();
    }
}
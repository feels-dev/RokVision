using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.ActionPoints;

namespace RoK.Ocr.Application.Features.ActionPoints.Neurons;

public partial class ApItemNeuron
{
    [GeneratedRegex(@"(?:Own|Owned|Possui|Possuído).*?(\d[\d\.,]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex QuantityRegex();

    public List<ApItemEntry> Extract(TopologyGraph graph, List<AnalyzedBlock> nodes)
    {
        var foundItems = new List<ApItemEntry>();

        foreach (var target in ApVocabulary.TargetItems)
        {
            var titleBlock = nodes
                .Where(n => RokCognitiveTools.CalculateSimilarity(n.Raw.Text, target.Name) > 0.85)
                .OrderByDescending(n => n.Raw.Confidence)
                .FirstOrDefault();

            if (titleBlock == null) continue;

            var qtyBlock = FindQuantityBlockRelative(titleBlock, nodes);

            if (qtyBlock != null)
            {
                int qty = ParseInt(qtyBlock.Raw.Text);
                if (qty > 0)
                {
                    // --- CHANGE: Normalize Confidence to 0-100 Scale ---
                    double rawConf = (titleBlock.Raw.Confidence + qtyBlock.Raw.Confidence) / 2;
                    double normalizedConf = Math.Round(rawConf * 100, 2); // 0.9698 -> 96.98

                    foundItems.Add(new ApItemEntry
                    {
                        ItemId = target.Id,
                        Name = target.Name,
                        UnitValue = target.Value,
                        Quantity = qty,
                        Confidence = normalizedConf
                    });
                }
            }
        }
        return foundItems;
    }

    private AnalyzedBlock? FindQuantityBlockRelative(AnalyzedBlock anchor, List<AnalyzedBlock> nodes)
    {
        double anchorHeight = anchor.Raw.Box[2][1] - anchor.Raw.Box[0][1];
        double anchorBottom = anchor.Raw.Box[2][1];
        double anchorLeft = anchor.Raw.Box[0][0];
        double anchorRight = anchor.Raw.Box[1][0];

        double searchY_Min = anchorBottom; 
        double searchY_Max = anchorBottom + (anchorHeight * 4.5); 

        return nodes
            .Where(n => n != anchor)
            .Where(n => n.Raw.Center.Y > searchY_Min && n.Raw.Center.Y < searchY_Max)
            .Where(n => n.Raw.Center.X >= anchorLeft - (anchorHeight * 2) && 
                        n.Raw.Center.X <= anchorRight + (anchorHeight * 2))
            .Where(n => QuantityRegex().IsMatch(n.Raw.Text))
            .OrderBy(n => n.Raw.Center.Y)
            .FirstOrDefault();
    }

    private int ParseInt(string text)
    {
        var match = QuantityRegex().Match(text);
        if (match.Success)
        {
            string clean = match.Groups[1].Value
                .Replace(".", "")
                .Replace(",", "")
                .Replace("l", "1")
                .Replace("I", "1")
                .Replace("O", "0");
                
            if (int.TryParse(clean, out int v)) return v;
        }
        return 0;
    }
}
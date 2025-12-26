using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Experience;

namespace RoK.Ocr.Application.Features.Experience.Neurons;

public partial class XpGridNeuron
{
    [GeneratedRegex(@"[^\d]", RegexOptions.Compiled)]
    private static partial Regex DigitsOnly();

    public List<XpItemEntry> Extract(List<AnalyzedBlock> nodes)
    {
        var foundItems = new List<XpItemEntry>();
        
        // 1. Identify ALL Anchors (Icons/Titles)
        var anchors = new List<(AnalyzedBlock Block, int Val, string Id)>();
        foreach (var node in nodes)
        {
            int val = ParseInt(node.Raw.Text);
            var match = XpVocabulary.TargetBooks.FirstOrDefault(t => t.XpValue == val);
            
            // Accept if value exists and color matches (or if strong value match regardless of color)
            if (match.XpValue > 0 && match.Colors.Contains(node.Raw.DominantColor))
            {
                anchors.Add((node, val, match.Id));
            }
        }

        if (!anchors.Any()) return foundItems;

        // --- DYNAMIC: UNIVERSAL SCALE CALCULATION ---
        // Calculates the median height of anchors to ignore OCR variations.
        var heights = anchors.Select(a => a.Block.Raw.Box[2][1] - a.Block.Raw.Box[0][1]).OrderBy(h => h).ToList();
        double medianHeight = heights[heights.Count / 2];
        
        // Defines limits based on Screen Average, not individual items.
        double searchRadiusY = medianHeight * 5.5; // Vertical search range
        double maxAlignDevX = medianHeight * 2.0;  // Allowed horizontal deviation

        // 2. Identify Quantity Candidates
        var anchorBlocksSet = anchors.Select(a => a.Block).ToHashSet();
        var potentialQuantities = nodes
            .Where(n => !anchorBlocksSet.Contains(n))
            .Where(n => ParseInt(n.Raw.Text) > 0)
            .Where(n => n.Raw.Text.Length < 9)
            .ToList();

        // 3. Matchmaking
        var matches = new List<CandidateMatch>();

        foreach (var anchor in anchors)
        {
            foreach (var qtyNode in potentialQuantities)
            {
                // Rule 1: Must be below anchor
                if (qtyNode.Raw.Center.Y <= anchor.Block.Raw.Center.Y) continue;

                // Rule 2: Horizontal Alignment (Using Universal Scale)
                double hDiff = Math.Abs(qtyNode.Raw.Center.X - anchor.Block.Raw.Center.X);
                if (hDiff > maxAlignDevX) continue;

                // Rule 3: Line of Sight (Vertical Blockage)
                bool isBlocked = anchors.Any(other => 
                    other.Block != anchor.Block &&
                    other.Block.Raw.Center.Y > anchor.Block.Raw.Center.Y && 
                    other.Block.Raw.Center.Y < qtyNode.Raw.Center.Y &&
                    Math.Abs(other.Block.Raw.Center.X - anchor.Block.Raw.Center.X) < maxAlignDevX // Same column
                );
                
                if (isBlocked) continue;

                // Rule 4: Distance check
                double dist = RokCognitiveTools.CalculateDistance(
                    anchor.Block.Raw.Center.X, anchor.Block.Raw.Center.Y,
                    qtyNode.Raw.Center.X, qtyNode.Raw.Center.Y);

                if (dist < searchRadiusY)
                {
                    matches.Add(new CandidateMatch 
                    { 
                        Anchor = anchor, 
                        QuantityBlock = qtyNode, 
                        Distance = dist 
                    });
                }
            }
        }

        // 4. Auction / Competition Logic
        var sortedMatches = matches.OrderBy(m => m.Distance).ToList();
        var usedAnchors = new HashSet<AnalyzedBlock>();
        var usedQuantities = new HashSet<AnalyzedBlock>();

        foreach (var match in sortedMatches)
        {
            if (usedAnchors.Contains(match.Anchor.Block) || usedQuantities.Contains(match.QuantityBlock))
                continue;

            int qty = ParseInt(match.QuantityBlock.Raw.Text);
            double conf = (match.Anchor.Block.Raw.Confidence + match.QuantityBlock.Raw.Confidence) / 2 * 100;
            
            foundItems.Add(new XpItemEntry
            {
                ItemId = match.Anchor.Id,
                UnitValue = match.Anchor.Val,
                Quantity = qty,
                Confidence = Math.Round(conf, 2),
                DetectedColor = match.Anchor.Block.Raw.DominantColor,
                AnchorBlock = match.Anchor.Block
            });

            usedAnchors.Add(match.Anchor.Block);
            usedQuantities.Add(match.QuantityBlock);
        }

        // 5. Orphaned Anchors (Pass Universal Scale to Magnifier later)
        foreach (var anchor in anchors)
        {
            if (!usedAnchors.Contains(anchor.Block))
            {
                // Leaving confidence as 0 to signal the Magnifier that this needs attention.
                foundItems.Add(new XpItemEntry
                {
                    ItemId = anchor.Id,
                    UnitValue = anchor.Val,
                    Quantity = -1,
                    Confidence = 0,
                    DetectedColor = anchor.Block.Raw.DominantColor + "_PENDING", 
                    AnchorBlock = anchor.Block
                });
            }
        }

        return foundItems;
    }

    private int ParseInt(string text)
    {
        string clean = DigitsOnly().Replace(text, "");
        return int.TryParse(clean, out int v) ? v : 0;
    }

    private class CandidateMatch
    {
        public (AnalyzedBlock Block, int Val, string Id) Anchor { get; set; }
        public required AnalyzedBlock QuantityBlock { get; set; }
        public double Distance { get; set; }
    }
}
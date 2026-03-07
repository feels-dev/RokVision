using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Reports.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;

namespace RoK.Ocr.Application.Features.Reports.Neurons;

/// <summary>
/// Specialized neuron for extracting Governor Names from Battle Reports.
/// optimized to distinguish between the Report Header and the actual Governor Name.
/// </summary>
public class GovernorNameNeuron
{
    private readonly List<CommanderEntry> _commanders;

    public GovernorNameNeuron(List<CommanderEntry> commanders)
    {
        _commanders = commanders;
    }

    /// <summary>
    /// Extracts the governor's name prioritizing tag slicing or spatial neighborhood.
    /// Includes logic to ignore the Report Header (centered tags).
    /// </summary>
    public ExtractionResult<string> Extract(
        TopologyGraph graph,
        AnalyzedBlock? tagNode,
        SideLocation side,
        List<AnalyzedBlock> allNodes,
        string suffix = "",
        AnalyzedBlock? blacklistBlock = null)
    {
        // 1. Tag Suffix Strategy (Strongest, but requires validation)
        if (!string.IsNullOrWhiteSpace(suffix) && IsValidPlayerName(suffix))
        {
            return new ExtractionResult<string>
            {
                Value = CleanName(suffix),
                Confidence = 90,
                Strategy = "Name_FromTagSuffix",
                SourceBlock = tagNode
            };
        }

        // 2. Tag Neighbor Strategy (With Header Protection)
        if (tagNode != null)
        {
            // CRITICAL FIX:
            // If we are looking for the Attacker (Left side), but the Tag is located 
            // too far to the right (> 0.35 Normalized X), it is likely the Report Header/Title.
            // We should ignore this tag and fall back to spatial search for the real name on the left.
            bool isLikelyHeader = side == SideLocation.Attacker && tagNode.NormalizedCenter.X > 0.35;

            if (!isLikelyHeader)
            {
                var neighbor = graph.FindNeighbor(tagNode, Direction.Right, 0.35);
                if (neighbor != null && neighbor != blacklistBlock && IsValidPlayerName(neighbor.Raw.Text))
                {
                    return new ExtractionResult<string>
                    {
                        Value = CleanName(neighbor.Raw.Text),
                        Confidence = neighbor.Raw.Confidence * 100,
                        Strategy = "Name_SpatialNeighbor",
                        SourceBlock = neighbor
                    };
                }
            }
        }

        // 3. Spatial Zone Fallback (Sniper Adjustment)
        // Defines specific zones for Attacker (Left) vs Defender (Right)

        double minX = side == SideLocation.Attacker ? 0.22 : 0.55;
        double maxX = side == SideLocation.Attacker ? 0.48 : 0.88;

        double minY = 0.08;
        double maxY = 0.28;

        var candidate = allNodes
            .Where(n => n != blacklistBlock)
            .Where(n => n.NormalizedCenter.X >= minX && n.NormalizedCenter.X <= maxX)
            .Where(n => n.NormalizedCenter.Y >= minY && n.NormalizedCenter.Y <= maxY)
            .Where(n => n.Type == BlockType.Unknown || n.Type == BlockType.Tag)
            .Where(n => IsValidPlayerName(n.Raw.Text))
            // Sort by Y first to pick the top-most valid text in the zone (Name comes before Power)
            .OrderBy(n => n.NormalizedCenter.Y)
            .FirstOrDefault();

        if (candidate != null)
        {
            return new ExtractionResult<string>
            {
                Value = CleanName(candidate.Raw.Text),
                Confidence = candidate.Raw.Confidence * 100,
                Strategy = "Name_SpatialZoneFallback",
                SourceBlock = candidate
            };
        }

        return new ExtractionResult<string> { Value = "--", Confidence = 0, Strategy = "Name_NotFound" };
    }

    private bool IsValidPlayerName(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2) return false;

        // Block dates/times (common noise in header area)
        if (Regex.IsMatch(text, @"\d{4}/\d{2}/\d{2}")) return false;
        if (Regex.IsMatch(text, @"\d{2}:\d{2}")) return false;

        // Block coordinates
        if (Regex.IsMatch(text, @"X:?\s*\d+", RegexOptions.IgnoreCase)) return false;

        // Block Power labels
        if (text.Contains("Poder", StringComparison.OrdinalIgnoreCase) || text.Contains("Power", StringComparison.OrdinalIgnoreCase)) return false;

        // Block Resource/Result keywords
        var pveTerms = new[] { "Restante", "Invasores", "Chefes", "Vago", "Mensagem", "Derrota", "Vitoria", "Defeat", "Victory", "Contagem", "Abates" };
        if (pveTerms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase))) return false;

        if (WarVocabulary.UiBlacklist.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase))) return false;

        return true;
    }

    private string CleanName(string text)
    {
        // Remove [TAG] residues
        string noTag = Regex.Replace(text, @"\[.*?\]", "").Trim();
        string clean = Regex.Replace(noTag, @"[\[\]]", "").Trim();

        // Remove trailing coordinates residues
        clean = Regex.Replace(clean, @"X:\d+.*", "", RegexOptions.IgnoreCase).Trim();

        return clean;
    }
}
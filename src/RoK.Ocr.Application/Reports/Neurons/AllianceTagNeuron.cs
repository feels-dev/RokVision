using System.Text.RegularExpressions;
using System.Linq; // Added explicitly just in case
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Application.Shared.Cognitive;

namespace RoK.Ocr.Application.Reports.Neurons;

public class AllianceTagResult
{
    public string Tag { get; set; } = "--";
    public string NameSuffix { get; set; } = "";
    public AnalyzedBlock? OriginalBlock { get; set; }
    public bool LowConfidence { get; set; }
}

public class AllianceTagNeuron
{
    public AllianceTagResult Extract(TopologyGraph graph, SideLocation side)
    {
        double minX = side == SideLocation.Attacker ? 0.0 : 0.5;
        double maxX = side == SideLocation.Attacker ? 0.5 : 1.0;

        // Searches in the header zone
        var nodes = graph.GetNodesInRegion(minX, maxX, 0.0, 0.4);

        // Looks for the best candidate that starts with [ or ends with ]
        var tagBlock = nodes
            .Where(n => n.Type != BlockType.UI)
            .Where(n => n.Raw.Text.Contains("[") || n.Raw.Text.Contains("]"))
            .OrderByDescending(n => n.Raw.Confidence)
            .FirstOrDefault();

        if (tagBlock == null) return new AllianceTagResult();

        return ParseRigid(tagBlock);
    }

    private AllianceTagResult ParseRigid(AnalyzedBlock block)
    {
        string text = block.Raw.Text.Trim();
        var result = new AllianceTagResult { OriginalBlock = block };

        // More permissive Regex: accepts almost anything inside brackets [ ]
        // from 2 to 6 characters.
        var matchA = Regex.Match(text, @"^\[(?<tag>[^\]]{2,6})\](?<name>.*)");
        if (matchA.Success)
        {
            result.Tag = matchA.Groups["tag"].Value;
            result.NameSuffix = matchA.Groups["name"].Value;
            return result;
        }

        // Case B: AAAA]Name (Lost the initial bracket)
        var matchB = Regex.Match(text, @"^(?<tag>.{3,5})\](?<name>.*)");
        if (matchB.Success)
        {
            result.Tag = matchB.Groups["tag"].Value;
            result.NameSuffix = matchB.Groups["name"].Value;
            return result;
        }

        // Case C: [AAAANOME (Stuck together and no closing bracket)
        if (text.StartsWith("["))
        {
            string content = text.Substring(1);
            if (content.Length >= 4)
            {
                result.Tag = content.Substring(0, 4);
                result.NameSuffix = content.Substring(4);
                result.LowConfidence = true;
                return result;
            }
        }

        // Case D: Pure Text that OCR classified as Tag
        result.Tag = text.Replace("[", "").Replace("]", "").Trim();
        if (result.Tag.Length > 5) result.Tag = result.Tag.Substring(0, 5);

        return result;
    }
}
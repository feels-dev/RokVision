using System.Text.RegularExpressions;
using System.Linq;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Application.Common.Cognitive;

namespace RoK.Ocr.Application.Features.Reports.Neurons;

public class AllianceTagResult
{
    public string Tag { get; set; } = "--";
    public string NameSuffix { get; set; } = "";
    public AnalyzedBlock? OriginalBlock { get; set; }
    public bool LowConfidence { get; set; }
    public string Strategy { get; set; } = "Unknown"; // New field
}

public class AllianceTagNeuron
{
    public AllianceTagResult Extract(TopologyGraph graph, SideLocation side)
    {
        double minX = side == SideLocation.Attacker ? 0.0 : 0.5;
        double maxX = side == SideLocation.Attacker ? 0.5 : 1.0;

        var nodes = graph.GetNodesInRegion(minX, maxX, 0.0, 0.4);

        var tagBlock = nodes
            .Where(n => n.Type != BlockType.UI)
            .Where(n => n.Raw.Text.Contains("[") || n.Raw.Text.Contains("]"))
            .Where(n => !(side == SideLocation.Attacker && n.NormalizedCenter.X > 0.35 && n.NormalizedCenter.Y < 0.22))
            .OrderByDescending(n => n.Raw.Confidence)
            .FirstOrDefault();

        if (tagBlock == null) return new AllianceTagResult { Strategy = "Tag_NotFound" };

        return ParseRigid(tagBlock);
    }

    private AllianceTagResult ParseRigid(AnalyzedBlock block)
    {
        string text = block.Raw.Text.Trim();
        var result = new AllianceTagResult { OriginalBlock = block };

        // Case A: [TAG] Name
        var matchA = Regex.Match(text, @"^\[(?<tag>[^\]]{2,6})\](?<name>.*)");
        if (matchA.Success)
        {
            result.Tag = matchA.Groups["tag"].Value;
            result.NameSuffix = matchA.Groups["name"].Value;
            result.Strategy = "Tag_Regex_Brackets";
            return result;
        }

        // Case B: TAG]Name
        var matchB = Regex.Match(text, @"^(?<tag>.{3,5})\](?<name>.*)");
        if (matchB.Success)
        {
            result.Tag = matchB.Groups["tag"].Value;
            result.NameSuffix = matchB.Groups["name"].Value;
            result.Strategy = "Tag_Regex_PartialBracket";
            return result;
        }

        // Case C: [TAGNAME
        if (text.StartsWith("["))
        {
            string content = text.Substring(1);
            if (content.Length >= 4)
            {
                result.Tag = content.Substring(0, 4);
                result.NameSuffix = content.Substring(4);
                result.LowConfidence = true;
                result.Strategy = "Tag_Heuristic_StartBracket";
                return result;
            }
        }

        // Case D: Pure Text
        result.Tag = text.Replace("[", "").Replace("]", "").Trim();
        if (result.Tag.Length > 5) result.Tag = result.Tag.Substring(0, 5);
        result.Strategy = "Tag_PureTextFallback";

        return result;
    }
}
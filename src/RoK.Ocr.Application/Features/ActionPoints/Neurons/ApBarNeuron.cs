using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.ActionPoints.Neurons;

public partial class ApBarNeuron
{
    // Flexible Regex: Matches "875/1,000", "944 / 1.700"
    [GeneratedRegex(@"(\d[\d,\.]*)\s*\/\s*(\d[\d,\.]*)", RegexOptions.Compiled)]
    private static partial Regex BarRegex();

    public (int Current, int Max) Extract(List<AnalyzedBlock> nodes)
    {
        // Scans the top 10 blocks (lowest Y value) to avoid scanning the whole list
        var topNodes = nodes.OrderBy(n => n.Raw.Box[0][1]).Take(10);

        foreach (var node in topNodes)
        {
            if (!node.Raw.Text.Contains("/")) continue;

            var match = BarRegex().Match(node.Raw.Text);
            if (match.Success)
            {
                int current = ParseInt(match.Groups[1].Value);
                int max = ParseInt(match.Groups[2].Value);

                // Sanity check: Max must be greater than 0
                if (max > 0) return (current, max);
            }
        }
        return (0, 0);
    }

    private int ParseInt(string val) 
        => int.TryParse(val.Replace(".", "").Replace(",", ""), out int res) ? res : 0;
}
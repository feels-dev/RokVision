using System.Globalization;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;
using RoK.Ocr.Domain.Enums;
using System.Linq;
using System.Collections.Generic;

namespace RoK.Ocr.Application.Features.Reports.Neurons;

public class PveMetricNeuron
{
    public PveDetails Extract(List<AnalyzedBlock> nodes, SideLocation side)
    {
        var details = new PveDetails();

        // Searches for the percentage block in the defender zone
        var percentBlock = nodes
            .Where(n => n.Raw.Text.Contains("%"))
            .FirstOrDefault(n => side == SideLocation.Defender ? n.NormalizedCenter.X > 0.5 : n.NormalizedCenter.X < 0.5);

        if (percentBlock != null)
        {
            // Extracts only the number (e.g., "-43,2%" -> 43.2)
            var match = Regex.Match(percentBlock.Raw.Text, @"(\d+[,.]\d+)");
            if (match.Success && double.TryParse(match.Value.Replace(",", "."), CultureInfo.InvariantCulture, out double val))
            {
                details.DamageReceivedPercentage = val;
            }
        }
        return details;
    }
}
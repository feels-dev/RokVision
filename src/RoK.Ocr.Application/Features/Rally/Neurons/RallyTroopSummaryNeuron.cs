using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Rally;

namespace RoK.Ocr.Application.Features.Rally.Neurons;

public partial class RallyTroopSummaryNeuron
{
    // Compiled regex for performance - Strips all non-digit characters
    [GeneratedRegex(@"[^\d]", RegexOptions.Compiled)]
    private static partial Regex NonDigitRegex();

    public void Extract(List<AnalyzedBlock> analyzedBlocks, RallyResult result, HashSet<AnalyzedBlock> usedBlocks, double topBoundaryY, double bottomBoundaryY, int imgW, int imgH)
    {
        // 1. Isolate all nodes within the target Y-axis band (Dark blue troop summary row)
        var nodesInBand = analyzedBlocks
            .Where(n => n.Raw.Box[0][1] / (double)imgH >= topBoundaryY && 
                        n.Raw.Box[0][1] / (double)imgH < bottomBoundaryY)
            .Except(usedBlocks)
            .ToList();

        if (!nodesInBand.Any()) return;

        // 2. Dynamic Content Window Calculation
        // Determines the actual horizontal bounds of the rendered text to avoid hardcoded constraints.
        double minPixelX = nodesInBand.Min(n => n.Raw.Box[0][0]);
        double maxPixelX = nodesInBand.Max(n => n.Raw.Box[2][0]); 
        double contentWidth = maxPixelX - minPixelX;

        // Safety fallback: If only a single valid number is detected resulting in an 
        // unrealistic width, fallback to the full image width.
        if (contentWidth < imgW * 0.4) 
        {
            minPixelX = 0;
            contentWidth = imgW;
        }

        // 3. Filter blocks containing valid troop counts
        var numericNodes = nodesInBand
            .Where(n => IsValidTroopNumber(n.Raw.Text))
            .OrderBy(n => n.Raw.Box[0][0]) // Strictly orders blocks from left to right
            .ToList();

        // 4. Dynamic Mapping via Relative Quadrants
        foreach (var node in numericNodes)
        {
            long val = ParseNumber(node.Raw.Text);
            
            // Ignores negative anomalies but allows "0" (Unsent troop types render as 0 in-game).
            if (val < 0) continue; 

            double centerX = node.NormalizedCenter.X * imgW;
            
            // Calculates relative position within the dynamic content window (0.00 to 1.00 range)
            double relativePosition = (centerX - minPixelX) / contentWidth;

            // Maps values to corresponding troop types based on strict 25% quadrant allocations
            if (relativePosition <= 0.25) 
            {
                result.GlobalTroops.Infantry = val;
            }
            else if (relativePosition > 0.25 && relativePosition <= 0.50) 
            {
                result.GlobalTroops.Cavalry = val;
            }
            else if (relativePosition > 0.50 && relativePosition <= 0.75) 
            {
                result.GlobalTroops.Archer = val;
            }
            else 
            {
                result.GlobalTroops.Siege = val;
            }

            usedBlocks.Add(node);
        }
    }

    private bool IsValidTroopNumber(string text)
    {
        if (!Regex.IsMatch(text, @"\d")) return false;
        
        // Collision Prevention: Rejects coordinates, capacity fractions, and timers
        if (text.Contains("X:") || text.Contains("Y:") || text.Contains("/")) return false;
        if (text.Contains(":")) return false; 
        
        // Collision Prevention: Rejects header text overlap
        if (text.Contains("Unidades", StringComparison.OrdinalIgnoreCase)) return false;

        // Validates character ratio; rejects blocks with excessive alphabetical characters 
        // mistakenly parsed as numbers due to OCR noise.
        string lettersOnly = Regex.Replace(text, @"[\d\.,KM\s]", "", RegexOptions.IgnoreCase);
        if (lettersOnly.Length > 3) return false;

        return true;
    }

    private long ParseNumber(string text)
    {
        return long.TryParse(NonDigitRegex().Replace(text, ""), out long val) ? val : 0;
    }
}
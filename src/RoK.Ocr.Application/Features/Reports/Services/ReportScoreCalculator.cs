using System;
using System.Collections.Generic;
using System.Linq;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;

namespace RoK.Ocr.Application.Features.Reports.Services;

public class ReportScoreCalculator
{
    public double Calculate(ReportResult result, List<AnalyzedBlock> usedNodes, bool isIsolated)
    {
        double score = 0;

        // 1. MATHEMATICAL PILLAR (Max: 50 points)
        // This is the strongest validator in RoK.
        if (result.IsMathematicallySound())
        {
            score += 50;
        }
        else
        {
            // If math doesn't add up, we calculate how far off it is.
            // Small difference (1 digit reading error) loses little.
            // Large difference loses a lot.
            long atkDiff = Math.Abs((result.Attacker.TotalUnits + result.Attacker.Healed) -
                           (result.Attacker.Dead + result.Attacker.SeverelyWounded +
                            result.Attacker.SlightlyWounded + result.Attacker.Remaining + result.Attacker.WatchtowerDamage));

            if (atkDiff > 0 && result.Attacker.TotalUnits > 0)
            {
                double errorRatio = (double)atkDiff / result.Attacker.TotalUnits;
                score += Math.Max(0, 25 * (1 - errorRatio)); // Gives up to 25 points if the error is tiny
            }
        }

        // 2. OCR PILLAR (Max: 20 points)
        // Average confidence of the blocks we actually used to fill the object
        if (usedNodes.Any())
        {
            double avgOcrConf = usedNodes.Average(n => n.Raw.Confidence) * 100;
            score += (avgOcrConf * 0.20);
        }

        // 3. SEMANTIC PILLAR (Max: 20 points)
        // We reward filling in essential fields
        int fieldsFound = 0;
        if (result.Attacker.GovernorName != "--") fieldsFound++;
        if (result.Attacker.AllianceTag != "--") fieldsFound++;
        if (result.Attacker.PrimaryCommander != null) fieldsFound++;
        if (result.Defender.GovernorName != "--") fieldsFound++;

        // Each essential field is worth 5 points
        score += (fieldsFound * 5);

        // 4. INFRASTRUCTURE PILLAR (Max: 10 points)
        if (isIsolated) score += 10;

        // --- PENALTIES ---
        // If attacker name wasn't found, it's a "blind" report
        if (result.Attacker.GovernorName == "--") score -= 20;

        if (result.Warnings.Any(w => w.Contains("DUPLICATE_NAMES") || w.Contains("HEADER_READ")))
        {
            score -= 40; // Heavy penalty for identity error
        }

        // Ensures score stays between 0 and 100
        return Math.Clamp(score, 0, 100);
    }
}
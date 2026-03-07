using System;
using RoK.Ocr.Domain.Models.Reports;
using RoK.Ocr.Application.Common.Models;

namespace RoK.Ocr.Application.Features.Reports.Services;

public static class ConsistencyAuditor
{
    public static void Audit(ReportResult report, OcrAnalysisContext context)
    {
        AuditSide(report.Attacker, "Attacker", context, "atk");

        if (report.Type != Domain.Enums.ReportType.Barbarian)
        {
            AuditSide(report.Defender, "Defender", context, "def");
        }
    }

    private static void AuditSide(BattleSide side, string sideName, OcrAnalysisContext context, string prefix)
    {
        if (side.TotalUnits <= 0) return;

        long expected = side.TotalUnits + side.Healed;
        long actual = side.Dead + side.SeverelyWounded + side.SlightlyWounded + side.Remaining + side.WatchtowerDamage;

        if (expected != actual)
        {
            long diff = Math.Abs(expected - actual);
            context.LogWarning(
                "ConsistencyAuditor", 
                "WARN_MATH_MISMATCH", 
                $"[{sideName}] Math mismatch: Expected {expected} vs Actual {actual} (Diff: {diff})", 
                "HIGH", 
                $"{prefix}_total_units"
            );
        }
    }
}
using System;
using RoK.Ocr.Domain.Models.Reports;
using RoK.Ocr.Domain.Enums;

namespace RoK.Ocr.Application.Features.Reports.Services;

public static class ConsistencyAuditor
{
    public static void Audit(ReportResult report)
    {
        // 1. Audit the Attacker
        AuditSide(report.Attacker, "Attacker", report);

        // 2. Audit the Defender (only if it is PVP)
        if (report.Type != ReportType.Barbarian)
        {
            AuditSide(report.Defender, "Defender", report);
        }
    }

    private static void AuditSide(BattleSide side, string sideName, ReportResult report)
    {
        if (side.TotalUnits <= 0) return;

        long expected = side.TotalUnits + side.Healed;
        long actual = side.Dead + side.SeverelyWounded + side.SlightlyWounded + side.Remaining + side.WatchtowerDamage;

        if (expected != actual)
        {
            long diff = Math.Abs(expected - actual);
            report.Warnings.Add($"[Math Mismatch] {sideName}: Difference of {diff} units.");
        }
    }
}
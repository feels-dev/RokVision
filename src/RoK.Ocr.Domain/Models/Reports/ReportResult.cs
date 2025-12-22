using RoK.Ocr.Domain.Enums;
using System;
using System.Collections.Generic;

namespace RoK.Ocr.Domain.Models.Reports;

public class ReportResult
{
    public ReportType Type { get; set; } = ReportType.Unknown;
    
    public BattleSide Attacker { get; set; } = new();
    public BattleSide Defender { get; set; } = new();

    public DateTime? Timestamp { get; set; }
    public string MapCoordinates { get; set; } = "--";

    public bool IsVictoryForAttacker => 
        Defender.Remaining == 0 && Attacker.Remaining > 0;

    public bool IsMathematicallySound() 
    {
        bool attackerOk = Attacker.IsValid();

        bool defenderOk = Type == ReportType.Barbarian || Defender.IsValid();
        
        return attackerOk && defenderOk;
    }

    public double OverallConfidence { get; set; }
    public List<string> Warnings { get; set; } = new();

    // public string RawText { get; set; } = string.Empty;
}
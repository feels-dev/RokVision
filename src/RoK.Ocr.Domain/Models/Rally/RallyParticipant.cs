using System.Collections.Generic;
using RoK.Ocr.Domain.Models.Reports;

namespace RoK.Ocr.Domain.Models.Rally;

public class RallyParticipant
{
    public string Name { get; set; } = "--";
    
    public bool IsLeader { get; set; }

    public CommanderEntry? PrimaryCommander { get; set; }
    public CommanderEntry? SecondaryCommander { get; set; }

    /// <summary>
    /// Status text extracted from the UI (e.g., "Arrived", "00:01:20").
    /// </summary>
    public string MarchStatus { get; set; } = "--";

    public long TotalUnits { get; set; }

    /// <summary>
    /// Details regarding the troop type and tier.
    /// Inferred via Color Detection (Python) + Column Position (C#).
    /// </summary>
    public List<RallyTroopDetail> TroopDetails { get; set; } = new();
}

public class RallyTroopDetail
{
    /// <summary>
    /// Infantry, Cavalry, Archer, Siege.
    /// </summary>
    public string Type { get; set; } = "Unknown";

    /// <summary>
    /// T1, T2, T3, T4, T5 (Inferred from background color).
    /// </summary>
    public string Tier { get; set; } = "Unknown";

    public long Count { get; set; }

    /// <summary>
    /// The raw color detected by Python (e.g., "Purple", "Gold").
    /// Useful for debugging the tier inference.
    /// </summary>
    public string DetectedColor { get; set; } = "Unknown";
}
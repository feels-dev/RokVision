using System.Collections.Generic;
using RoK.Ocr.Domain.Models.Reports;
namespace RoK.Ocr.Domain.Models.Rally;

/// <summary>
/// Root object representing the full analysis of an Alliance Rally.
/// Aggregates data from multiple screenshots (scroll).
/// </summary>
public class RallyResult
{
    /// <summary>
    /// Synthetic ID composed of LeaderCoords_TargetCoords (e.g., "X103Y796_X98Y800").
    /// Used to deduplicate rallies in databases.
    /// </summary>
    public string RallyId { get; set; } = string.Empty;

    public RallyParty Leader { get; set; } = new();

    public RallyTarget Target { get; set; } = new();

    public RallyStatus Status { get; set; } = new();

    /// <summary>
    /// The summary of troop types (Infantry, Cavalry, etc.) usually found in the header.
    /// </summary>
    public RallyTroopsSummary GlobalTroops { get; set; } = new();

    /// <summary>
    /// List of players participating in the rally.
    /// Extracted from the scrollable list.
    /// </summary>
    public List<RallyParticipant> Participants { get; set; } = new();

    /// <summary>
    /// Global confidence score based on OCR quality and mathematical consistency checks.
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// List of warnings or inconsistencies found during audit (e.g., "Capacity mismatch").
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

public class RallyParty
{
    public string Name { get; set; } = "--";
    public string AllianceTag { get; set; } = "--";
    public int X { get; set; }
    public int Y { get; set; }
}

public class RallyTarget
{
    public string Name { get; set; } = "--";
    
    /// <summary>
    /// Level of the target (e.g., Barbarian Fort Lv. 4).
    /// </summary>
    public int Level { get; set; }
    
    /// <summary>
    /// Indicates if the target is a known NPC (Barbarian, Fort, Lohar, etc.).
    /// </summary>
    public bool IsNpc { get; set; }
    
    public int X { get; set; }
    public int Y { get; set; }
}

public class RallyStatus
{
    public string State { get; set; } = "--"; // e.g., "Preparing", "Marching"
    public string TimeRemaining { get; set; } = "--"; // e.g., "00:04:49"
    
    public long CurrentCapacity { get; set; }
    public long MaxCapacity { get; set; }
    
    public double FillPercentage => MaxCapacity > 0 
        ? System.Math.Round((double)CurrentCapacity / MaxCapacity * 100, 2) 
        : 0;
}

public class RallyTroopsSummary
{
    public long Infantry { get; set; }
    public long Cavalry { get; set; }
    public long Archer { get; set; }
    public long Siege { get; set; }
}
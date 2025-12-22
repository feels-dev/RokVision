namespace RoK.Ocr.Domain.Models.Reports;

public class BattleSide
{
    public string GovernorName { get; set; } = "--";
    public string AllianceTag { get; set; } = "--";

    // NOVO: Flag para identificar se este lado é um Bárbaro/Forte/Boss
    public bool IsNpc { get; set; } = false;

    public CommanderEntry? PrimaryCommander { get; set; }
    public CommanderEntry? SecondaryCommander { get; set; }

    public long TotalUnits { get; set; }
    public long Healed { get; set; }
    public long Dead { get; set; }
    public long SeverelyWounded { get; set; }
    public long SlightlyWounded { get; set; }
    public long Remaining { get; set; }
    public long KillPointsGained { get; set; }
    public long WatchtowerDamage { get; set; }
    public PveDetails? PveStats { get; set; }

    public double CasualtyRate => TotalUnits > 0
        ? (double)(Dead + SeverelyWounded) / TotalUnits
        : 0;

    public bool IsValid()
    {
        if (TotalUnits <= 0) return false;
        long sum = Dead + SeverelyWounded + SlightlyWounded + Remaining + WatchtowerDamage;
        return Math.Abs((TotalUnits + Healed) - sum) <= 1;
    }

}

public class PveDetails
{
    public double DamageReceivedPercentage { get; set; } // O "-43,2%" do print
    public int EntityLevel { get; set; } // O "10" do "Nv. 10"
    public string EntityType { get; set; } = "Barbarian"; // Barbarian, Fort, Sanctum Guardian
}
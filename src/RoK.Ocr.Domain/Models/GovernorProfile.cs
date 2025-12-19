namespace RoK.Ocr.Domain.Models;

public class GovernorProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "--";
    public string AllianceTag { get; set; } = "--";
    public string AllianceName { get; set; } = "--";
    public long Power { get; set; }
    public long KillPoints { get; set; }
    public string Civilization { get; set; } = "--";

    // Flag to determine whether auditing needs to be triggered or not
    public bool IsSuccessfulRead { get; set; }
}
using System.Collections.Generic;
using System.Linq;

namespace RoK.Ocr.Domain.Models.Experience;

public class XpInventoryData
{
    public long TotalXp => Items.Sum(i => i.TotalXp);
    public List<XpItemEntry> Items { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    // public List<string> BlockDebug { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
}

public class XpItemEntry
{
    public string ItemId { get; set; } = string.Empty;
    public int UnitValue { get; set; }
    public int Quantity { get; set; }
    public long TotalXp => (long)UnitValue * Quantity;
    public double Confidence { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public AnalyzedBlock? AnchorBlock { get; set; }
    public string DetectedColor { get; set; } = "Unknown";
}
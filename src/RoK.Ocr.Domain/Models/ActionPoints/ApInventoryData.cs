using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Domain.Models.ActionPoints;

public class ApInventoryData
{
    public long GrandTotalAp => Items.Sum(i => i.TotalValue);

    public int CurrentBarValue { get; set; }
    public int MaxBarValue { get; set; }

    public List<ApItemEntry> Items { get; set; } = new();
}

public class ApItemEntry
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int UnitValue { get; set; }
    public int Quantity { get; set; }
    public long TotalValue => (long)Quantity * UnitValue;
    public double Confidence { get; set; }
    public string? Strategy { get; set; } = "Default";

    [JsonIgnore]
    public AnalyzedBlock? AnchorBlock { get; set; }
}
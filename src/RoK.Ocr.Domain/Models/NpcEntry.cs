using System.Collections.Generic;

namespace RoK.Ocr.Domain.Models;

public class NpcEntry
{
    public string Id { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public string Rarity { get; set; } = "Common";
    public string Expertise { get; set; } = "NPC";
    public List<string> Labels { get; set; } = new();
}
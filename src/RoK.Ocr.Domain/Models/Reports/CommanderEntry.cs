namespace RoK.Ocr.Domain.Models.Reports;

public class CommanderEntry
{
    public string Id { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    
    // Metadados estratégicos (Ouro para o futuro!)
    public string Rarity { get; set; } = "Legendary"; // Legendary, Epic, Elite
    public string Expertise { get; set; } = "Mixed";  // Cavalry, Infantry, Archer, etc.
}
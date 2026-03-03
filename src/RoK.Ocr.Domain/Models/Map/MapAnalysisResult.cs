using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Domain.Models.Map;

public class MapAnalysisResult
{
    // The Kingdom number (e.g., #3746)
    public int KingdomNumber { get; set; }
    
    // X Coordinate on the map
    public int X { get; set; }
    
    // Y Coordinate on the map
    public int Y { get; set; }
    
    // List of identified cities in the screenshot
    public List<MapCity> Cities { get; set; } = new();
}

public class MapCity
{
    // The name of the governor (e.g., "DDFeels")
    public string Name { get; set; } = string.Empty;
    
    // The alliance tag (e.g., "Ab46"), empty if none
    public string AllianceTag { get; set; } = string.Empty;
    
    // Indicates if a shield bubble was detected above the city
    public bool HasShield { get; set; }
    
    // The center coordinates of the city on the screen (useful for clicking)
    public ScreenLocationDto ScreenLocation { get; set; } = new();

    // Internal helper for debug drawing, ignored in JSON response
    [JsonIgnore]
    public List<List<double>>? DebugBox { get; set; }
}

public class ScreenLocationDto
{
    public double Cx { get; set; }
    public double Cy { get; set; }

    public ScreenLocationDto() { }
    public ScreenLocationDto(double x, double y) { Cx = x; Cy = y; }
}
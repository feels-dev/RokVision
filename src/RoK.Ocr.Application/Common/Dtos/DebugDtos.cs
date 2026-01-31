using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

public class DebugInformationDto
{
    // Raw full text (heavy, only filled if requested)
    [JsonPropertyName("rawText")]
    public string? RawText { get; set; }

    [JsonPropertyName("image")]
    public ImageMetaDto? Image { get; set; }

    // Execution timings (ms)
    [JsonPropertyName("timings")]
    public Dictionary<string, double> Timings { get; set; } = new();

    // Anchors found by the orchestrator for localization
    [JsonPropertyName("anchorsFound")]
    public List<string> AnchorsFound { get; set; } = new();

    // Details if the Magnifier had to run
    [JsonPropertyName("magnifier")]
    public List<MagnifierDebugInfo> Magnifier { get; set; } = new();
}

public class ImageMetaDto
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("originalWidth")]
    public int Width { get; set; }

    [JsonPropertyName("originalHeight")]
    public int Height { get; set; }
    
    [JsonPropertyName("resizeScale")]
    public double ResizeScale { get; set; }
}

public class MagnifierDebugInfo
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("strategiesTried")]
    public int StrategiesTried { get; set; }

    [JsonPropertyName("winningStrategy")]
    public string? WinningStrategy { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
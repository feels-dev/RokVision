using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

public class DebugInformationDto
{
    /// <summary>
    /// Raw full text extracted by the OCR engine. Heavy property, only filled if requested.
    /// </summary>
    [JsonPropertyName("rawText")]
    public string? RawText { get; set; }

    [JsonPropertyName("imagePath")]
    public string? ImagePath { get; set; }

    /// <summary>
    /// Execution timings in milliseconds mapped by component.
    /// </summary>
    [JsonPropertyName("timings")]
    public Dictionary<string, double> Timings { get; set; } = new();

    /// <summary>
    /// Anchors or spatial references found by the orchestrator for localization.
    /// </summary>
    [JsonPropertyName("anchorsFound")]
    public List<string> AnchorsFound { get; set; } = new();

    /// <summary>
    /// Details about magnification attempts.
    /// </summary>
    [JsonPropertyName("magnifier")]
    public List<MagnifierDebugInfo> Magnifier { get; set; } = new();

    /// <summary>
    /// Object detection metrics provided by the YOLO model.
    /// </summary>
    [JsonPropertyName("yoloMetrics")]
    public Dictionary<string, object> YoloMetrics { get; set; } = new();
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
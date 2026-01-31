using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Represents extraction metadata and evidence for a specific field.
/// </summary>
public class FieldEvidenceDto
{
    /// <summary>
    /// Sanitized final value (e.g., 15000000).
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    /// <summary>
    /// Raw text read by OCR before cleanup (e.g., "15.OOO.OOO").
    /// </summary>
    [JsonPropertyName("raw")]
    public string Raw { get; set; } = string.Empty;

    /// <summary>
    /// Read confidence (0.0 to 100.0).
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Neuron or strategy that obtained this result (e.g., "StatsNeuron_Magnifier").
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = "Unknown";

    /// <summary>
    /// Bounding box where text was found [x, y, w, h]. Optional.
    /// </summary>
    [JsonPropertyName("box")]
    public List<int>? Box { get; set; }

    /// <summary>
    /// Indicates if any auto-correction was applied (e.g., Fuzzy Match, Math Fix).
    /// </summary>
    [JsonPropertyName("isCorrection")]
    public bool IsCorrection { get; set; }
}
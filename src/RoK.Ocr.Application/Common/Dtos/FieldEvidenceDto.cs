using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Identifies the engines or heuristics responsible for finding and reading the data.
/// </summary>
public class ExtractionSourceDto
{
    /// <summary>
    /// The system that located the region of interest (e.g., "YOLO_Region_Power", "Heuristic_Spatial_Anchor").
    /// </summary>
    [JsonPropertyName("detector")]
    public string Detector { get; set; } = "Unknown";

    /// <summary>
    /// The system that actually read the content inside the region (e.g., "PaddleOCR_v4").
    /// </summary>
    [JsonPropertyName("reader")]
    public string Reader { get; set; } = "Unknown";
}

/// <summary>
/// Represents extraction metadata and evidence for a specific text-based field.
/// </summary>
public class FieldEvidenceDto
{
    /// <summary>
    /// Sanitized final value processed by the business logic (e.g., 15000000).
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    /// <summary>
    /// Raw text read by the OCR engine before cleanup (e.g., "15.OOO.OOO").
    /// </summary>[JsonPropertyName("rawText")]
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// Read confidence score ranging from 0.0 to 100.0.
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Indicates if any auto-correction was applied (e.g., Fuzzy Match, Math Fix, Consistency Auditor).
    /// </summary>
    [JsonPropertyName("isCorrection")]
    public bool IsCorrection { get; set; }

    /// <summary>
    /// Details about the models or algorithms used to extract this field.
    /// </summary>
    [JsonPropertyName("source")]
    public ExtractionSourceDto Source { get; set; } = new();

    /// <summary>
    /// Geometric data defining where this field is located in the original image.
    /// </summary>
    [JsonPropertyName("spatial")]
    public SpatialContextDto Spatial { get; set; } = new();
}
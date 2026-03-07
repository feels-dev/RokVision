using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Standardized Enterprise response for all RoK Vision API operations.
/// </summary>
/// <typeparam name="T">Type of returned business data (e.g., GovernorProfile, ReportResult)</typeparam>
public class RokResponse<T>
{
    [JsonPropertyName("meta")]
    public MetaDto Meta { get; set; } = new();

    [JsonPropertyName("status")]
    public StatusDto Status { get; set; } = new();

    [JsonPropertyName("data")]
    public DataEnvelope<T>? Data { get; set; }

    /// <summary>
    /// Chronological history of decisions, neural network steps, and automated fixes.
    /// Replaces the legacy flat AuditLog with a structured telemetry approach.
    /// </summary>
    [JsonPropertyName("executionTrace")]
    public ExecutionTraceDto ExecutionTrace { get; set; } = new();

    /// <summary>
    /// Optional debug information (image dimensions, raw text, etc).
    /// Intended for developer troubleshooting only.
    /// </summary>[JsonPropertyName("debug")]
    public DebugInformationDto? Debug { get; set; }
}
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Standardized response for all RoK Vision API operations.
/// </summary>
/// <typeparam name="T">Type of returned data (e.g., GovernorProfile, ReportResult)</typeparam>
public class RokResponse<T>
{
    [JsonPropertyName("meta")]
    public MetaDto Meta { get; set; } = new();

    [JsonPropertyName("status")]
    public StatusDto Status { get; set; } = new();

    [JsonPropertyName("data")]
    public DataEnvelope<T>? Data { get; set; }

    /// <summary>
    /// Chronological history of OCR decisions (Audit Trail).
    /// </summary>
    [JsonPropertyName("auditLog")]
    public List<string> AuditLog { get; set; } = new();

    /// <summary>
    /// Optional debug info (image dimensions, raw text, etc).
    /// </summary>
    [JsonPropertyName("debug")]
    public object? Debug { get; set; }
}
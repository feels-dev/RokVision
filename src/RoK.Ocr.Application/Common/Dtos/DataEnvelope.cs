using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Generic envelope separating the simple domain model from technical extraction details.
/// </summary>
/// <typeparam name="T">The domain model type (e.g., GovernorProfile, ReportResult)</typeparam>
public class DataEnvelope<T>
{
    /// <summary>
    /// The clean domain object ready for use.
    /// </summary>
    [JsonPropertyName("summary")]
    public T Summary { get; set; } = default!;

    /// <summary>
    /// Technical details, confidence, and evidence for each extracted field.
    /// The dictionary key is the field name (e.g., "power", "id").
    /// </summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, FieldEvidenceDto> Fields { get; set; } = new();
}
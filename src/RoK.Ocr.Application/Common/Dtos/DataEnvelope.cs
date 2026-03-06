using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Generic envelope separating the clean domain model from technical extraction details and UI elements.
/// </summary>
/// <typeparam name="T">The domain model type (e.g., GovernorProfile, ReportResult)</typeparam>
public class DataEnvelope<T>
{
    /// <summary>
    /// The clean domain object ready for business use.
    /// </summary>[JsonPropertyName("businessSummary")]
    public T BusinessSummary { get; set; } = default!;

    /// <summary>
    /// Technical details, confidence, and spatial evidence for each extracted text field.
    /// The dictionary key is the field name (e.g., "power", "id").
    /// </summary>[JsonPropertyName("extractedFields")]
    public Dictionary<string, FieldEvidenceDto> ExtractedFields { get; set; } = new();

    /// <summary>
    /// A collection of UI elements (buttons, icons) detected in the image, 
    /// providing exact coordinates for automation tools to interact with.
    /// </summary>[JsonPropertyName("interactables")]
    public Dictionary<string, InteractableElementDto> Interactables { get; set; } = new();
}
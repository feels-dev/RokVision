using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Represents a UI element detected in the image (e.g., Buttons, Icons, Flags) 
/// intended for interaction by automation tools or bots.
/// </summary>
public class InteractableElementDto
{
    /// <summary>
    /// Detection confidence score ranging from 0.0 to 100.0.
    /// </summary>[JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// The model or system that detected this element (e.g., "YOLOv8_UI_Model").
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "Unknown";

    /// <summary>
    /// Geometric data defining where this element is located in the original image.
    /// </summary>
    [JsonPropertyName("spatial")]
    public SpatialContextDto Spatial { get; set; } = new();
}
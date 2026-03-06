using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Represents a 2D point in the image, usually the exact click target for automation bots.
/// </summary>
public class PointDto
{[JsonPropertyName("x")]
    public int? X { get; set; }

    [JsonPropertyName("y")]
    public int? Y { get; set; }

    [JsonPropertyName("nx")]
    public double? NormalizedX { get; set; }

    [JsonPropertyName("ny")]
    public double? NormalizedY { get; set; }
}

/// <summary>
/// Represents a bounding box in the image.
/// </summary>
public class BoundingBoxDto
{
    [JsonPropertyName("x")]
    public int? X { get; set; }

    [JsonPropertyName("y")]
    public int? Y { get; set; }

    [JsonPropertyName("w")]
    public int? Width { get; set; }

    [JsonPropertyName("h")]
    public int? Height { get; set; }[JsonPropertyName("nx")]
    public double? NormalizedX { get; set; }

    [JsonPropertyName("ny")]
    public double? NormalizedY { get; set; }

    [JsonPropertyName("nw")]
    public double? NormalizedWidth { get; set; }

    [JsonPropertyName("nh")]
    public double? NormalizedHeight { get; set; }
}

/// <summary>
/// Contains absolute pixel coordinates. Highly dependent on the original image resolution.
/// </summary>
public class AbsoluteSpatialDto
{
    [JsonPropertyName("box")]
    public BoundingBoxDto Box { get; set; } = new();

    [JsonPropertyName("center")]
    public PointDto Center { get; set; } = new();
}

/// <summary>
/// Contains normalized coordinates (0.0 to 1.0). 
/// Resolution independent, highly recommended for building robust automation bots.
/// </summary>
public class NormalizedSpatialDto
{
    [JsonPropertyName("box")]
    public BoundingBoxDto Box { get; set; } = new();

    [JsonPropertyName("center")]
    public PointDto Center { get; set; } = new();
}

/// <summary>
/// The complete spatial context for any detected element, combining both absolute and normalized metrics.
/// </summary>
public class SpatialContextDto
{
    [JsonPropertyName("absolute")]
    public AbsoluteSpatialDto Absolute { get; set; } = new();

    [JsonPropertyName("normalized")]
    public NormalizedSpatialDto Normalized { get; set; } = new();
}
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

// DTOs for Absolute (Pixel) Coordinates
public class AbsolutePointDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

public class AbsoluteBoundingBoxDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("w")]
    public int Width { get; set; }

    [JsonPropertyName("h")]
    public int Height { get; set; }
}

public class AbsoluteSpatialDto
{
    [JsonPropertyName("box")]
    public AbsoluteBoundingBoxDto? Box { get; set; }

    [JsonPropertyName("center")]
    public AbsolutePointDto? Center { get; set; }
}

// DTOs for Normalized (0.0 to 1.0) Coordinates
public class NormalizedPointDto
{
    [JsonPropertyName("nx")]
    public double NormalizedX { get; set; }

    [JsonPropertyName("ny")]
    public double NormalizedY { get; set; }
}

public class NormalizedBoundingBoxDto
{
    [JsonPropertyName("nx")]
    public double NormalizedX { get; set; }

    [JsonPropertyName("ny")]
    public double NormalizedY { get; set; }

    [JsonPropertyName("nw")]
    public double NormalizedWidth { get; set; }

    [JsonPropertyName("nh")]
    public double NormalizedHeight { get; set; }
}

public class NormalizedSpatialDto
{
    [JsonPropertyName("box")]
    public NormalizedBoundingBoxDto? Box { get; set; }

    [JsonPropertyName("center")]
    public NormalizedPointDto? Center { get; set; }
}

// The complete spatial context
public class SpatialContextDto
{
    [JsonPropertyName("absolute")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AbsoluteSpatialDto? Absolute { get; set; }

    [JsonPropertyName("normalized")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NormalizedSpatialDto? Normalized { get; set; }
}
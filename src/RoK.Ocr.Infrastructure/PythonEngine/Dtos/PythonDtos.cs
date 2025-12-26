using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Infrastructure.PythonEngine.Dtos;

// --- OCR RESPONSE ---
public class PythonOcrResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("full_text")]
    public string FullText { get; set; } = string.Empty;

    [JsonPropertyName("blocks")]
    public List<PythonBlockDto> Blocks { get; set; } = new();

    [JsonPropertyName("detail")]
    public string? ErrorDetail { get; set; }
}

public class PythonBlockDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("conf")]
    public double Confidence { get; set; }

    // Python returns [[x,y], [x,y]...]
    [JsonPropertyName("box")]
    public List<List<double>> Box { get; set; } = new();
    [JsonPropertyName("color")]
    public string Color { get; set; } = "Unknown";
}

// --- REPORT RESPONSE ---
public class PythonReportResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("blocks")] public List<PythonBlockDto> Blocks { get; set; } = new();
    [JsonPropertyName("container")] public PythonContainerDto Container { get; set; } = null!;
    [JsonPropertyName("processed_image_path")] public string ProcessedImagePath { get; set; } = string.Empty;
}

public class PythonContainerDto
{
    [JsonPropertyName("is_isolated")]
    public bool IsIsolated { get; set; }

    [JsonPropertyName("canvas_size")]
    public CanvasSizeDto CanvasSize { get; set; } = null!;
}

public class CanvasSizeDto
{
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
}

public class PythonBatchResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("results")]
    public List<BatchItemResult> Results { get; set; } = new();
}

public class BatchItemResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("conf")]
    public double Confidence { get; set; }

    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = string.Empty;
}
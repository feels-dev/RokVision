using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Infrastructure.PythonEngine;

// Represents the raw JSON response from Python
public class PythonOcrResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("full_text")]
    public string FullText { get; set; } = string.Empty;

    [JsonPropertyName("blocks")]
    public List<PythonBlockDto> Blocks { get; set; } = new();
    
    [JsonPropertyName("detail")] // Used to capture FastAPI errors
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
}
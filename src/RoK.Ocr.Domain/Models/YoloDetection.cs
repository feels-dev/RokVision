using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Domain.Models;

public class YoloDetection
{
    [JsonPropertyName("class")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("box")]
    public List<int> Box { get; set; } = new(); // [x, y, w, h]
}
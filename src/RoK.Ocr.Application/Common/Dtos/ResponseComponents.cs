using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

public class MetaDto
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "1.0.0";

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("engine")]
    public string Engine { get; set; } = "RoK.Ocr.Engine v1.0";
}

public class StatusDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; } = 200;

    [JsonPropertyName("message")]
    public string Message { get; set; } = "Completed.";

    /// <summary>
    /// Total processing time in seconds.
    /// </summary>
    [JsonPropertyName("processingTimeSeconds")]
    public double ProcessingTimeSeconds { get; set; }

    /// <summary>
    /// Global confidence score for the result (0-100).
    /// </summary>
    [JsonPropertyName("overallConfidence")]
    public double OverallConfidence { get; set; }

    [JsonPropertyName("warnings")]
    public List<SystemWarning> Warnings { get; set; } = new();
}

public class SystemWarning
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "WARN_GENERIC";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("field")]
    public string? TargetField { get; set; }

    public SystemWarning(string code, string message, string? targetField = null)
    {
        Code = code;
        Message = message;
        TargetField = targetField;
    }
}
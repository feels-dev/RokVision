using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoK.Ocr.Application.Common.Dtos;

public class ImageContextDto
{[JsonPropertyName("originalWidth")]
    public int OriginalWidth { get; set; }[JsonPropertyName("originalHeight")]
    public int OriginalHeight { get; set; }

    [JsonPropertyName("resizedScale")]
    public double ResizedScale { get; set; } = 1.0;
}

public class MetaDto
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "2.0.0";

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("engine")]
    public string Engine { get; set; } = "RoKVision-Core (YOLO + PaddleOCR)";[JsonPropertyName("imageContext")]
    public ImageContextDto ImageContext { get; set; } = new();
}

public class StatusDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("httpCode")]
    public int HttpCode { get; set; } = 200;

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Global confidence score for the result (0.0 to 100.0).
    /// </summary>
    [JsonPropertyName("overallConfidence")]
    public double OverallConfidence { get; set; }

    [JsonPropertyName("warnings")]
    public List<SystemWarning> Warnings { get; set; } = new();
}

public class SystemWarning
{[JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    /// <summary>
    /// Indicates the severity level: LOW, MEDIUM, HIGH, CRITICAL.
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; }

    [JsonPropertyName("targetField")]
    public string? TargetField { get; set; }

    public SystemWarning(string code, string message, string severity = "MEDIUM", string? targetField = null)
    {
        Code = code;
        Message = message;
        Severity = severity;
        TargetField = targetField;
    }
}

public class ExecutionStepDto
{[JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Standard log levels: INFO, WARN, ERROR.
    /// </summary>[JsonPropertyName("level")]
    public string Level { get; set; } = "INFO";

    /// <summary>
    /// The internal component that generated this step (e.g., "YOLO_Engine", "ConsistencyAuditor").
    /// </summary>
    [JsonPropertyName("component")]
    public string Component { get; set; } = "Unknown";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class ExecutionTraceDto
{
    [JsonPropertyName("isPerfectMatch")]
    public bool IsPerfectMatch { get; set; }

    [JsonPropertyName("magnifierUsed")]
    public bool MagnifierUsed { get; set; }

    [JsonPropertyName("steps")]
    public List<ExecutionStepDto> Steps { get; set; } = new();
}
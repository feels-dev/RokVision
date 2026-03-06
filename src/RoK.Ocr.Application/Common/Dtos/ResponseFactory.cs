using System;
using System.Collections.Generic;
using RoK.Ocr.Application.Common.Models;

namespace RoK.Ocr.Application.Common.Dtos;

/// <summary>
/// Factory responsible for building standard API responses, mapping telemetry and business data.
/// </summary>
public static class ResponseFactory
{
    public static RokResponse<T> CreateSuccess<T>(
        T businessSummary,
        OcrAnalysisContext context,
        string correlationId,
        ImageContextDto imageContext)
    {
        return new RokResponse<T>
        {
            Meta = new MetaDto
            {
                CorrelationId = correlationId,
                ImageContext = imageContext
            },
            Status = new StatusDto
            {
                Success = true,
                HttpCode = 200,
                ExecutionTimeMs = context.GetTotalProcessingTimeMs(),
                OverallConfidence = CalculateOverallConfidence(context.ExtractedFields),
                Warnings = context.Warnings
            },
            Data = new DataEnvelope<T>
            {
                BusinessSummary = businessSummary,
                ExtractedFields = context.ExtractedFields,
                Interactables = context.Interactables
            },
            ExecutionTrace = context.ExecutionTrace
            // Note: 'Debug' property mapping is typically handled at the Controller level 
            // depending on the incoming request flags (?debug=true).
        };
    }

    public static RokResponse<T> CreateFailure<T>(string message, string errorCode = "ERR_INTERNAL", int httpCode = 500, string correlationId = "")
    {
        var trace = new ExecutionTraceDto();
        trace.Steps.Add(new ExecutionStepDto 
        { 
            Level = "ERROR", 
            Component = "System_Gateway", 
            Message = message 
        });

        return new RokResponse<T>
        {
            Meta = new MetaDto { CorrelationId = string.IsNullOrEmpty(correlationId) ? Guid.NewGuid().ToString() : correlationId },
            Status = new StatusDto
            {
                Success = false,
                HttpCode = httpCode,
                ExecutionTimeMs = 0,
                Warnings = new List<SystemWarning> 
                { 
                    new SystemWarning(errorCode, message, "CRITICAL") 
                }
            },
            Data = null,
            ExecutionTrace = trace
        };
    }

    private static double CalculateOverallConfidence(Dictionary<string, FieldEvidenceDto> fields)
    {
        if (fields == null || fields.Count == 0) return 0;
        
        double sum = 0;
        foreach (var f in fields.Values) sum += f.Confidence;
        
        return Math.Round(sum / fields.Count, 2);
    }
}
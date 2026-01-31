using System;
using System.Collections.Generic;

namespace RoK.Ocr.Application.Common.Dtos;

public static class ResponseFactory
{
    public static RokResponse<T> CreateSuccess<T>(
        T summary, 
        Dictionary<string, FieldEvidenceDto> fields, 
        List<string> auditLog, 
        double processingTime,
        double overallConfidence = 100,
        List<SystemWarning>? warnings = null)
    {
        return new RokResponse<T>
        {
            Meta = new MetaDto(), // Automatically generates timestamp and ID
            Status = new StatusDto
            {
                Success = true,
                Code = 200,
                Message = "Analysis completed successfully.",
                ProcessingTimeSeconds = processingTime,
                OverallConfidence = overallConfidence,
                Warnings = warnings ?? new List<SystemWarning>()
            },
            Data = new DataEnvelope<T>
            {
                Summary = summary,
                Fields = fields
            },
            AuditLog = auditLog
        };
    }

    public static RokResponse<T> CreateFailure<T>(string message, string errorCode = "ERR_INTERNAL", int httpCode = 500)
    {
        return new RokResponse<T>
        {
            Meta = new MetaDto(),
            Status = new StatusDto
            {
                Success = false,
                Code = httpCode,
                Message = message,
                ProcessingTimeSeconds = 0,
                Warnings = new List<SystemWarning> 
                { 
                    new SystemWarning(errorCode, message) 
                }
            },
            Data = null,
            AuditLog = new List<string> { $"[CRITICAL] {message}" }
        };
    }
}
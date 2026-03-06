using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Common.Models;

/// <summary>
/// Context object that travels through the entire OCR and YOLO analysis pipeline,
/// collecting evidence, telemetry, traces, and metrics.
/// </summary>
public class OcrAnalysisContext
{
    // --- CORE DATA ---
    public ExecutionTraceDto ExecutionTrace { get; } = new();
    public Dictionary<string, FieldEvidenceDto> ExtractedFields { get; } = new();
    public Dictionary<string, InteractableElementDto> Interactables { get; } = new();
    public List<SystemWarning> Warnings { get; } = new();
    public DateTime StartTime { get; } = DateTime.UtcNow;

    // --- IMAGE CONTEXT (Required for Normalized Coordinates) ---
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }

    // --- DEBUG EXTENSIONS ---
    public DebugInformationDto DebugInfo { get; } = new();
    private readonly Dictionary<string, Stopwatch> _activeTimers = new();

    // --- TIMING METHODS ---
    public void StartTimer(string key)
    {
        if (_activeTimers.ContainsKey(key)) _activeTimers.Remove(key);
        _activeTimers[key] = Stopwatch.StartNew();
    }

    public void StopTimer(string key)
    {
        if (_activeTimers.ContainsKey(key))
        {
            var sw = _activeTimers[key];
            sw.Stop();
            if (DebugInfo.Timings.ContainsKey(key))
                DebugInfo.Timings[key] += sw.Elapsed.TotalMilliseconds;
            else
                DebugInfo.Timings[key] = sw.Elapsed.TotalMilliseconds;
            
            _activeTimers.Remove(key);
        }
    }

    // --- TRACING METHODS (Replacing flat string AuditLog) ---
    public void AddTrace(string level, string component, string message)
    {
        ExecutionTrace.Steps.Add(new ExecutionStepDto
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Component = component,
            Message = message
        });
    }

    public void Log(string component, string message) => AddTrace("INFO", component, message);
    
    public void LogWarning(string component, string code, string message, string severity = "MEDIUM", string? field = null)
    {
        AddTrace("WARN", component, $"{code}: {message}");
        Warnings.Add(new SystemWarning(code, message, severity, field));
    }
    
    public void LogError(string component, string message) => AddTrace("ERROR", component, message);

    // --- RESULT REGISTRATION METHODS ---
    public void RegisterResult<T>(string fieldKey, ExtractionResult<T> result, string detectorComponent, string readerComponent = "PaddleOCR_v4", bool isCorrection = false)
    {
        var evidence = new FieldEvidenceDto
        {
            Value = result.Value,
            RawText = result.SourceBlock?.Raw.Text ?? string.Empty,
            Confidence = Math.Clamp(Math.Round(result.Confidence, 2), 0, 100),
            IsCorrection = isCorrection,
            Source = new ExtractionSourceDto { Detector = detectorComponent, Reader = readerComponent },
            Spatial = CreateSpatialContext(result.SourceBlock)
        };

        if (ExtractedFields.ContainsKey(fieldKey))
        {
            Log("Context", $"Overwriting field '{fieldKey}'. Conf: {ExtractedFields[fieldKey].Confidence} -> {evidence.Confidence}");
            ExtractedFields[fieldKey] = evidence;
        }
        else
        {
            ExtractedFields.Add(fieldKey, evidence);
        }
        Log("Context", $"Field '{fieldKey}' set to '{result.Value}' via {detectorComponent}");
    }

    public void RegisterInteractable(string elementKey, double confidence, string detectorComponent, AnalyzedBlock block)
    {
        var interactable = new InteractableElementDto
        {
            Confidence = Math.Clamp(Math.Round(confidence, 2), 0, 100),
            Source = detectorComponent,
            Spatial = CreateSpatialContext(block)
        };

        Interactables[elementKey] = interactable;
        Log("Context", $"Interactable UI Element '{elementKey}' registered via {detectorComponent}");
    }

    private SpatialContextDto CreateSpatialContext(AnalyzedBlock? block)
    {
        var spatial = new SpatialContextDto();
        if (block == null) return spatial;

        try
        {
            var rawBox = block.Raw.Box;
            int x = (int)rawBox[0][0];
            int y = (int)rawBox[0][1];
            int w = (int)(rawBox[1][0] - rawBox[0][0]);
            int h = (int)(rawBox[2][1] - rawBox[1][1]);

            spatial.Absolute.Box = new BoundingBoxDto { X = x, Y = y, Width = w, Height = h };
            spatial.Absolute.Center = new PointDto { X = x + (w / 2), Y = y + (h / 2) };

            // Calculate normalized coordinates if image dimensions are provided
            if (ImageWidth > 0 && ImageHeight > 0)
            {
                spatial.Normalized.Box = new BoundingBoxDto
                {
                    NormalizedX = Math.Round((double)x / ImageWidth, 4),
                    NormalizedY = Math.Round((double)y / ImageHeight, 4),
                    NormalizedWidth = Math.Round((double)w / ImageWidth, 4),
                    NormalizedHeight = Math.Round((double)h / ImageHeight, 4)
                };
                spatial.Normalized.Center = new PointDto
                {
                    NormalizedX = Math.Round((double)spatial.Absolute.Center.X / ImageWidth, 4),
                    NormalizedY = Math.Round((double)spatial.Absolute.Center.Y / ImageHeight, 4)
                };
            }
        }
        catch { /* Ignore invalid box arrays */ }

        return spatial;
    }

    public double GetTotalProcessingTimeMs()
    {
        if (DebugInfo.Timings.TryGetValue("TotalOrchestration", out double val))
            return val; 
            
        return (DateTime.UtcNow - StartTime).TotalMilliseconds;
    }

    // --- DEBUG REGISTRATION ---
    public void RegisterAnchors(IEnumerable<string> keys)
    {
        if (keys != null) DebugInfo.AnchorsFound.AddRange(keys);
    }

    public void RegisterMagnifierAttempt(string field, int tries, string? winner, bool success)
    {
        DebugInfo.Magnifier.Add(new MagnifierDebugInfo
        {
            Field = field,
            StrategiesTried = tries,
            WinningStrategy = winner ?? "None",
            Success = success
        });
    }
}
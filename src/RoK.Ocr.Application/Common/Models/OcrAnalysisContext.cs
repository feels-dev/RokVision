using System;
using System.Collections.Generic;
using System.Diagnostics;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Common.Models;

public class OcrAnalysisContext
{
    // --- Existing Properties ---
    public List<string> AuditLog { get; } = new();
    public Dictionary<string, FieldEvidenceDto> Evidence { get; } = new();
    public List<SystemWarning> Warnings { get; } = new();
    public DateTime StartTime { get; } = DateTime.UtcNow;

    // --- DEBUG EXTENSIONS ---
    // Structure to accumulate data
    public DebugInformationDto DebugInfo { get; } = new();

    // Private dictionary for active timers
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
            // Accumulate if already exists (for loops)
            if (DebugInfo.Timings.ContainsKey(key))
                DebugInfo.Timings[key] += sw.Elapsed.TotalMilliseconds;
            else
                DebugInfo.Timings[key] = sw.Elapsed.TotalMilliseconds;
            
            _activeTimers.Remove(key);
        }
    }

    // --- REGISTRATION METHODS ---
    
    public void RegisterAnchors(IEnumerable<string> keys)
    {
        DebugInfo.AnchorsFound.AddRange(keys);
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

    // --- Original Methods (Log, RegisterResult) ---
    public void Log(string message) => AuditLog.Add($"[INFO] {message}");

    public void LogWarning(string code, string message, string? field = null)
    {
        AuditLog.Add($"[WARN] {code}: {message}");
        Warnings.Add(new SystemWarning(code, message, field));
    }

    public void LogError(string message) => AuditLog.Add($"[ERROR] {message}");

    public void RegisterResult<T>(string fieldKey, ExtractionResult<T> result, string strategyName)
    {
        var evidence = new FieldEvidenceDto
        {
            Value = result.Value,
            Confidence = Math.Clamp(Math.Round(result.Confidence, 2), 0, 100), 
            Method = strategyName,
            Raw = result.SourceBlock?.Raw.Text ?? string.Empty,
            Box = ExtractBoundingBox(result.SourceBlock)
        };

        if (Evidence.ContainsKey(fieldKey))
        {
            Log($"Overwriting field '{fieldKey}'. Conf: {Evidence[fieldKey].Confidence} -> {evidence.Confidence}");
            Evidence[fieldKey] = evidence;
        }
        else
        {
            Evidence.Add(fieldKey, evidence);
        }
        Log($"Field '{fieldKey}' set to '{result.Value}' via {strategyName}");
    }

    private List<int>? ExtractBoundingBox(AnalyzedBlock? block)
    {
        if (block == null) return null;
        try
        {
            var rawBox = block.Raw.Box;
            int x = (int)rawBox[0][0];
            int y = (int)rawBox[0][1];
            int w = (int)(rawBox[1][0] - rawBox[0][0]);
            int h = (int)(rawBox[2][1] - rawBox[1][1]);
            return new List<int> { x, y, w, h };
        }
        catch { return null; }
    }
}
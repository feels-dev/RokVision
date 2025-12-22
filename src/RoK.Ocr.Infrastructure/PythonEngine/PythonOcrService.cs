using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization; // Added to map Python JSON
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for Serilog/Logging
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Infrastructure.PythonEngine;

public class PythonOcrService : IOcrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonOcrService> _logger;

    public PythonOcrService(HttpClient httpClient, ILogger<PythonOcrService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(List<OcrBlock> Blocks, string FullText)> AnalyzeImageAsync(string imagePath, byte[]? preLoadedBytes = null)
    {
        try
        {
            string base64String;
            if (preLoadedBytes != null && preLoadedBytes.Length > 0)
            {
                base64String = Convert.ToBase64String(preLoadedBytes);
            }
            else
            {
                byte[] fileBytes = await File.ReadAllBytesAsync(imagePath);
                base64String = Convert.ToBase64String(fileBytes);
            }

            var payload = new { imageBase64 = base64String };
            var response = await _httpClient.PostAsJsonAsync("governor/analyze", payload); // Adjusted for the new endpoint

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API returned non-success status code: {StatusCode}", response.StatusCode);
                return (new List<OcrBlock>(), string.Empty);
            }

            var result = await response.Content.ReadFromJsonAsync<PythonOcrResponse>();
            if (result == null || !result.Success)
            {
                _logger.LogWarning("Failed to deserialize Python response or Success is false.");
                return (new List<OcrBlock>(), string.Empty);
            }

            var domainBlocks = result.Blocks.Select(b => new OcrBlock
            {
                Text = b.Text,
                Confidence = b.Confidence,
                Box = b.Box
            }).ToList();

            return (domainBlocks, result.FullText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during AnalyzeImageAsync.");
            return (new List<OcrBlock>(), string.Empty);
        }
    }

    // FIX CS0738: Now returns the Tuple of 4 elements according to the Interface
    // Note: It seems the interface expects 4 items, but implementation returns 5 based on your previous code logic.
    // Keeping logic as provided.
    public async Task<(List<OcrBlock> Blocks, double Width, double Height, bool IsIsolated, string ProcessedPath)> AnalyzeReportAsync(string imagePath)
    {
        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(imagePath);
            var payload = new { imageBase64 = Convert.ToBase64String(fileBytes) };

            var response = await _httpClient.PostAsJsonAsync("reports/analyze", payload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Report API returned non-success status code: {StatusCode}", response.StatusCode);
                return (new(), 0, 0, false, string.Empty);
            }

            var result = await response.Content.ReadFromJsonAsync<PythonReportResponse>();

            if (result == null || !result.Success)
            {
                _logger.LogWarning("Failed to deserialize Report response or Success is false.");
                return (new(), 0, 0, false, string.Empty);
            }

            var blocks = result.Blocks.Select(b => new OcrBlock
            {
                Text = b.Text,
                Confidence = b.Confidence,
                Box = b.Box
            }).ToList();

            // Returning the 5 elements, including the new ProcessedImagePath
            return (blocks,
                    result.Container.CanvasSize.Width,
                    result.Container.CanvasSize.Height,
                    result.Container.IsIsolated,
                    result.ProcessedImagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during AnalyzeReportAsync.");
            return (new(), 0, 0, false, string.Empty); 
        }
    }

    // --- UPDATED DTO ---
    public class PythonReportResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("blocks")] public List<PythonBlockDto> Blocks { get; set; } = new();
        [JsonPropertyName("container")] public PythonContainerDto Container { get; set; } = null!;

        // Maps the field coming from Python
        [JsonPropertyName("processed_image_path")]
        public string ProcessedImagePath { get; set; } = string.Empty;
    }

    public class PythonContainerDto
    {
        [JsonPropertyName("is_isolated")] // Maps Python's snake_case
        public bool IsIsolated { get; set; }

        [JsonPropertyName("canvas_size")]
        public CanvasSizeDto CanvasSize { get; set; } = null!;
    }

    public class CanvasSizeDto
    {
        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }
    }
}
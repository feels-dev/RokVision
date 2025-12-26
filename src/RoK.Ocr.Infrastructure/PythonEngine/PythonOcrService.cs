using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Infrastructure.PythonEngine.Dtos; // Using the DTOs created above

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
            // Calls the Generic OCR endpoint
            var response = await _httpClient.PostAsJsonAsync("governor/analyze", payload);

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

            // Map DTO to Domain
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

    public async Task<(List<OcrBlock> Blocks, double Width, double Height, bool IsIsolated, string ProcessedPath)> AnalyzeReportAsync(string imagePath)
    {
        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(imagePath);
            var payload = new { imageBase64 = Convert.ToBase64String(fileBytes) };

            // Calls the Report OCR endpoint
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

    public async Task<List<OcrBlock>> AnalyzeBatchAsync(string imagePath, List<(string Id, int[] Box, string Strategy)> regions)
    {
        try
        {
            // Reads the image from disk ONCE
            byte[] fileBytes = await File.ReadAllBytesAsync(imagePath);
            string base64String = Convert.ToBase64String(fileBytes);

            var payload = new
            {
                imageBase64 = base64String,
                regions = regions.Select(r => new
                {
                    id = r.Id,
                    box = r.Box, // [x, y, w, h]
                    strategy = r.Strategy
                }).ToList()
            };

            // SINGLE POST Request
            var response = await _httpClient.PostAsJsonAsync("batch/process", payload);

            if (!response.IsSuccessStatusCode) return new List<OcrBlock>();

            var result = await response.Content.ReadFromJsonAsync<PythonBatchResult>();
            if (result == null || !result.Success) return new List<OcrBlock>();

            // Maps back to OcrBlock
            return result.Results.Select(r => new OcrBlock
            {
                CustomId = r.Id, // <--- Map here
                Text = r.Text,
                Confidence = r.Confidence,
                Box = new List<List<double>>()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Batch Analysis");
            return new List<OcrBlock>();
        }
    }

    public async Task<(List<OcrBlock> Blocks, string FullText)> AnalyzeInventoryAsync(string imagePath)
    {
        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(imagePath);
            string base64String = Convert.ToBase64String(fileBytes);

            var payload = new { imageBase64 = base64String };

            // Calls the new route /inventory/analyze
            var response = await _httpClient.PostAsJsonAsync("inventory/analyze", payload);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Inventory API returned error: {StatusCode}", response.StatusCode);
                return (new List<OcrBlock>(), string.Empty);
            }

            var result = await response.Content.ReadFromJsonAsync<PythonOcrResponse>();
            if (result == null || !result.Success) return (new List<OcrBlock>(), string.Empty);

            var domainBlocks = result.Blocks.Select(b => new OcrBlock
            {
                Text = b.Text,
                Confidence = b.Confidence,
                Box = b.Box,
                DominantColor = b.Color // Mapping
            }).ToList();

            return (domainBlocks, result.FullText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnalyzeInventoryAsync");
            return (new List<OcrBlock>(), string.Empty);
        }
    }
}
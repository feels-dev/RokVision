using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Infrastructure.PythonEngine;

public class PythonOcrService : IOcrService
{
    private readonly HttpClient _httpClient;

    public PythonOcrService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // The base address will be configured in Program.cs (e.g., http://localhost:8000)
    }

    public async Task<(List<OcrBlock> Blocks, string FullText)> AnalyzeImageAsync(string imagePath, byte[]? preLoadedBytes = null)
    {
        try
        {
            string base64String;

            // If the Controller has already sent the optimized bytes, use them (FAST)
            if (preLoadedBytes != null && preLoadedBytes.Length > 0)
            {
                base64String = Convert.ToBase64String(preLoadedBytes);
            }
            else
            {
                // Fallback: Read from disk (SLOW)
                byte[] fileBytes = await File.ReadAllBytesAsync(imagePath);
                base64String = Convert.ToBase64String(fileBytes);
            }

            var payload = new { imageBase64 = base64String };
            var response = await _httpClient.PostAsJsonAsync("process", payload);

            if (!response.IsSuccessStatusCode) return (new List<OcrBlock>(), string.Empty);

            var result = await response.Content.ReadFromJsonAsync<PythonOcrResponse>();
            if (result == null || !result.Success) return (new List<OcrBlock>(), string.Empty);

            var domainBlocks = result.Blocks.Select(b => new OcrBlock
            {
                Text = b.Text,
                Confidence = b.Confidence,
                Box = b.Box
            }).ToList();

            return (domainBlocks, result.FullText);
        }
        catch
        {
            return (new List<OcrBlock>(), string.Empty);
        }
    }
}
using RoK.Ocr.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RoK.Ocr.Domain.Interfaces;

public interface IOcrService
{
    Task<(List<OcrBlock> Blocks, string FullText)> AnalyzeImageAsync(string imagePath, byte[]? preLoadedBytes = null);
    Task<(List<OcrBlock> Blocks, double Width, double Height, bool IsIsolated, string ProcessedPath)> AnalyzeReportAsync(string imagePath);

    // NEW BATCH METHOD
    Task<List<OcrBlock>> AnalyzeBatchAsync(string imagePath, List<(string Id, int[] Box, string Strategy)> regions);
}
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Domain.Interfaces;

public interface IOcrService
{
    Task<(List<OcrBlock> Blocks, string FullText)> AnalyzeImageAsync(string imagePath, byte[]? preLoadedBytes = null);
    Task<(List<OcrBlock> Blocks, double Width, double Height, bool IsIsolated, string ProcessedPath)> AnalyzeReportAsync(string imagePath);
    Task<List<OcrBlock>> AnalyzeBatchAsync(string imagePath, List<(string Id, int[] Box, string Strategy)> regions);
    Task<(List<OcrBlock> Blocks, string FullText)> AnalyzeInventoryAsync(string imagePath);
    Task<List<YoloDetection>> GetMapDetectionsAsync(Stream imageStream, string fileName);
}
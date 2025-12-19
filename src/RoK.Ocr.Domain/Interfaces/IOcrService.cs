using System.Collections.Generic;
using System.Threading.Tasks;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Domain.Interfaces;

public interface IOcrService
{
    Task<(List<OcrBlock> Blocks, string FullText)> AnalyzeImageAsync(string imagePath, byte[]? preLoadedBytes = null);
}
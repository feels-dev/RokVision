using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace RoK.Ocr.Application.Features.Governor.Services;

public class GovernorMagnifier
{
    private readonly IOcrService _ocrService;
    private readonly IImageStorage _storage;
    private readonly string _webRoot; 
    private readonly string _tempPath;
    private readonly string _debugPath;
    
    private const bool EnableDebugMode = true; 

    public GovernorMagnifier(IOcrService ocrService, IImageStorage storage)
    {
        _ocrService = ocrService;
        _storage = storage;
        _webRoot = _storage.GetBasePath();
        
        _tempPath = Path.Combine(_webRoot, "uploads", "temp_crops");
        _debugPath = Path.Combine(_webRoot, "uploads", "debug_crops");
        
        if (!Directory.Exists(_tempPath)) Directory.CreateDirectory(_tempPath);
        if (EnableDebugMode && !Directory.Exists(_debugPath)) Directory.CreateDirectory(_debugPath);
    }

    public async Task<List<OcrBlock>> HuntForField(string imagePath, AnalyzedBlock anchor, string fieldType)
    {
        var strategies = GetHuntingStrategies(fieldType);
        
        var anchorRect = new Rect(
            anchor.Raw.Box[0][0], 
            anchor.Raw.Box[0][1], 
            anchor.Raw.Box[1][0] - anchor.Raw.Box[0][0], 
            anchor.Raw.Box[2][1] - anchor.Raw.Box[0][1]
        );

        string huntId = DateTime.Now.ToString("HHmmss");
        
        Console.WriteLine($"\n[GovernorMagnifier] --- Starting Hunt for: {fieldType} ---");

        foreach (var strategy in strategies)
        {
            string cropPathAbsolute = "";
            try
            {
                Console.WriteLine($"[GovernorMagnifier] Trying strategy: {strategy.Name}");

                var roi = strategy.CalculateRegion(anchorRect);
                
                cropPathAbsolute = await CreateSmartCrop(imagePath, roi, strategy);

                if (string.IsNullOrEmpty(cropPathAbsolute)) 
                {
                    Console.WriteLine("[GovernorMagnifier] Failure: Invalid crop region.");
                    continue;
                }

                string relativePath = Path.GetRelativePath(_webRoot, cropPathAbsolute);

                var result = await _ocrService.AnalyzeImageAsync(relativePath);
                
                bool isSuccess = false;
                string readText = "EMPTY";

                if (result.Blocks.Count > 0)
                {
                    readText = result.FullText.Replace("\n", " ").Trim();
                    Console.WriteLine($"[GovernorMagnifier] OCR Read: '{readText}'");

                    if (result.Blocks.Count > 0 && readText.Length >= 3)
                    {
                        isSuccess = true;
                        Console.WriteLine("[GovernorMagnifier] >> MATCH! Valid text found.");
                    }
                }
                else
                {
                    Console.WriteLine($"[GovernorMagnifier] OCR Failure");
                }

                if (EnableDebugMode)
                {
                    string filenameSafe = CleanFileName(readText);
                    string status = isSuccess ? "HIT" : "MISS";
                    string debugFilename = $"{huntId}_{fieldType}_{strategy.Name}_{status}_[{filenameSafe}].png";
                    string debugDest = Path.Combine(_debugPath, debugFilename);
                    
                    File.Copy(cropPathAbsolute, debugDest, true);
                    Console.WriteLine($"[GovernorMagnifier] Debug saved: {debugFilename}");
                }

                if (isSuccess)
                {
                    return RemapCoordinates(result.Blocks, roi, strategy.ScaleFactor);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GovernorMagnifier] ERROR: {ex.Message}");
                if (EnableDebugMode) File.WriteAllText(Path.Combine(_debugPath, $"{huntId}_ERROR.txt"), ex.Message);
                continue;
            }
            finally
            {
                // Temporary file cleanup
                if (!string.IsNullOrEmpty(cropPathAbsolute) && File.Exists(cropPathAbsolute)) 
                {
                    try { File.Delete(cropPathAbsolute); } catch { }
                }
            }
        }

        Console.WriteLine("[GovernorMagnifier] --- End of Hunt (Unsuccessful) ---\n");
        return new List<OcrBlock>();
    }

    private List<SearchStrategy> GetHuntingStrategies(string fieldType)
    {
        var list = new List<SearchStrategy>();

        if (fieldType == "Civilization")
        {
            list.Add(new SearchStrategy
            {
                Name = "1_Civ_Inverted_Binary",
                ScaleFactor = 2.0, 
                CalculateRegion = (r) => new Rect(r.X - 20, r.Y - 15, r.Width + 800, 300),
                ApplyFilters = (ctx) => 
                {
                    ctx.Resize(ctx.GetCurrentSize().Width * 2, ctx.GetCurrentSize().Height * 2);
                    ctx.Invert();
                    ctx.Grayscale();
                    ctx.BinaryThreshold(0.6f); 
                }
            });

            list.Add(new SearchStrategy
            {
                Name = "2_Civ_Normal_Sharpen",
                ScaleFactor = 2.0,
                CalculateRegion = (r) => new Rect(r.X - 20, r.Y - 15, r.Width + 800, 300),
                ApplyFilters = (ctx) => 
                {
                    ctx.Resize(ctx.GetCurrentSize().Width * 2, ctx.GetCurrentSize().Height * 2);
                    ctx.GaussianSharpen(1.0f); 
                }
            });
        }
        else if (fieldType == "Power" || fieldType == "KillPoints")
        {
            list.Add(new SearchStrategy {
                Name = "1_Stat_Below_Binary",
                ScaleFactor = 2.0,
                CalculateRegion = (r) => new Rect(r.X - 20, r.Y + (r.Height * 0.5), r.Width + 300, 300),
                ApplyFilters = (ctx) => {
                    ctx.Resize(ctx.GetCurrentSize().Width * 2, ctx.GetCurrentSize().Height * 2);
                    ctx.Grayscale();
                    ctx.BinaryThreshold(0.5f); 
                }
            });

            list.Add(new SearchStrategy {
                Name = "2_Stat_Wide_Contrast",
                ScaleFactor = 1.5,
                CalculateRegion = (r) => new Rect(r.X - 50, r.Y - 10, r.Width + 400, 300),
                ApplyFilters = (ctx) => {
                    ctx.Resize((int)(ctx.GetCurrentSize().Width * 1.5), (int)(ctx.GetCurrentSize().Height * 1.5));
                    ctx.Grayscale();
                    ctx.Contrast(1.5f);
                }
            });
        }
        else if (fieldType == "Name")
        {
             list.Add(new SearchStrategy {
                Name = "1_Name_Panorama",
                ScaleFactor = 2.0,
                CalculateRegion = (r) => new Rect(r.X - 150, r.Y + r.Height, r.Width + 500, 300),
                ApplyFilters = (ctx) => ctx.Resize(ctx.GetCurrentSize().Width * 2, ctx.GetCurrentSize().Height * 2)
            });
        }
        else if (fieldType == "Alliance")
        {
            list.Add(new SearchStrategy {
                Name = "1_Alliance_Panorama",
                ScaleFactor = 2.0,
                CalculateRegion = (r) => new Rect(r.X - 50, r.Y + (r.Height * 0.5), r.Width + 500, 300),
                ApplyFilters = (ctx) => ctx.Resize(ctx.GetCurrentSize().Width * 2, ctx.GetCurrentSize().Height * 2)
            });
        }

        return list;
    }

    private async Task<string> CreateSmartCrop(string path, Rect roi, SearchStrategy strategy)
    {
        string cropFile = Path.Combine(_tempPath, $"{Guid.NewGuid()}_{strategy.Name}.png");

        using (var image = Image.Load(path))
        {
            int x = Math.Max(0, (int)roi.X);
            int y = Math.Max(0, (int)roi.Y);
            int w = Math.Min((int)roi.Width, image.Width - x);
            int h = Math.Min((int)roi.Height, image.Height - y);
            
            if (w <= 10 || h <= 10) return string.Empty;

            image.Mutate(ctx => 
            {
                ctx.Crop(new Rectangle(x, y, w, h));
                strategy.ApplyFilters(ctx); 
            });

            await image.SaveAsPngAsync(cropFile);
        }
        return cropFile;
    }

    private List<OcrBlock> RemapCoordinates(List<OcrBlock> rawBlocks, Rect roi, double scaleFactor)
    {
        var newBlocks = new List<OcrBlock>();
        foreach (var raw in rawBlocks)
        {
            double factor = scaleFactor > 0 ? scaleFactor : 1;
            
            double localX1 = raw.Box[0][0] / factor;
            double localY1 = raw.Box[0][1] / factor;
            double localX2 = raw.Box[2][0] / factor;
            double localY2 = raw.Box[2][1] / factor;

            var globalBox = new List<List<double>>
            {
                new() { roi.X + localX1, roi.Y + localY1 },
                new() { roi.X + localX2, roi.Y + localY1 },
                new() { roi.X + localX2, roi.Y + localY2 },
                new() { roi.X + localX1, roi.Y + localY2 }
            };

            newBlocks.Add(new OcrBlock
            {
                Text = raw.Text,
                Confidence = raw.Confidence,
                Box = globalBox
            });
        }
        return newBlocks;
    }

    private string CleanFileName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        string clean = new string(text.Where(ch => !invalid.Contains(ch) && ch != '\n' && ch != '\r').ToArray());
        return clean.Length > 25 ? clean.Substring(0, 25) : clean;
    }

    private class SearchStrategy
    {
        public string Name { get; set; } = "Default";
        public double ScaleFactor { get; set; } = 1.0; 
        public Func<Rect, Rect> CalculateRegion { get; set; } 
        public Action<IImageProcessingContext> ApplyFilters { get; set; }
    }

    private record Rect(double X, double Y, double Width, double Height);
} 
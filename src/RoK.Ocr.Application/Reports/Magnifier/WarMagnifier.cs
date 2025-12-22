using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for Serilog/Logging
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace RoK.Ocr.Application.Reports.Magnifier;

public class WarMagnifier
{
    private readonly IOcrService _ocrService;
    private readonly string _tempPath;
    private readonly ILogger<WarMagnifier> _logger; // Logger added

    public WarMagnifier(IOcrService ocrService, IImageStorage storage, ILogger<WarMagnifier> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
        _tempPath = Path.Combine(storage.GetBasePath(), "uploads", "war_crops");
        
        if (!Directory.Exists(_tempPath)) 
            Directory.CreateDirectory(_tempPath);
    }

    /// <summary>
    /// Rescans a specific zone of the original image applying multiple processing strategies.
    /// </summary>
    public async Task<List<OcrBlock>> RescanZoneAsync(string imagePath, List<List<double>> box, string fieldName)
    {
        // Digital Chemotherapy Strategies
        var strategies = new List<(string Name, Action<IImageProcessingContext> Action)>
        {
            ("HighContrastBinary", ctx => { ctx.Grayscale(); ctx.BinaryThreshold(0.6f); }),
            ("SharpenAndContrast", ctx => { ctx.Contrast(1.5f); ctx.GaussianSharpen(1.5f); }),
            ("InvertedBinary", ctx => { ctx.Invert(); ctx.BinaryThreshold(0.4f); })
        };

        foreach (var strategy in strategies)
        {
            string cropFile = Path.Combine(_tempPath, $"retry_{fieldName}_{strategy.Name}_{Guid.NewGuid()}.png");
            
            try
            {
                using (var image = await Image.LoadAsync(imagePath))
                {
                    // Defines the region with a small safety "padding"
                    int x = (int)Math.Max(0, box[0][0] - 15);
                    int y = (int)Math.Max(0, box[0][1] - 8);
                    int w = (int)(box[2][0] - box[0][0] + 30);
                    int h = (int)(box[2][1] - box[0][1] + 16);

                    // Ensures the crop does not go out of image bounds
                    if (x + w > image.Width) w = image.Width - x;
                    if (y + h > image.Height) h = image.Height - y;

                    image.Mutate(ctx => {
                        ctx.Crop(new Rectangle(x, y, w, h));
                        ctx.Resize(w * 3, h * 3); // Upscale to facilitate reading small numbers
                        strategy.Action(ctx);
                    });

                    await image.SaveAsPngAsync(cropFile);
                }

                // OCR only on the processed crop
                var (blocks, _) = await _ocrService.AnalyzeImageAsync(cropFile);
                
                // Immediate cleanup
                if (File.Exists(cropFile)) File.Delete(cropFile);

                // If OCR returned something containing digits, we assume success in this strategy
                if (blocks.Any(b => System.Text.RegularExpressions.Regex.IsMatch(b.Text, @"\d")))
                {
                    // MASTER TOUCH: Strategy success log
                    _logger.LogInformation("[WarMagnifier] Success with strategy {StrategyName} for field: {FieldName}", strategy.Name, fieldName);
                    return blocks;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WarMagnifier] Error in strategy {StrategyName}: {Message}", strategy.Name, ex.Message);
                if (File.Exists(cropFile)) File.Delete(cropFile);
            }
        }

        return new List<OcrBlock>(); // Returns empty if no strategy succeeded
    }
}
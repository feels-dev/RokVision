using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Common.Models;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace RoK.Ocr.Application.Features.Map.Services;

public class MapMagnifier
{
    private readonly IOcrService _ocrService;
    private readonly IImageStorage _storage;

    public MapMagnifier(IOcrService ocrService, IImageStorage storage)
    {
        _ocrService = ocrService;
        _storage = storage;
    }

    /// <summary>
    /// Divides the image into slices to improve YOLO detection of small objects (Shields/Cities).
    /// </summary>
    public async Task<List<YoloDetection>> PerformSlicedDetectionAsync(string imagePath, int width, int height, string originalFileName)
    {
        // If the image is small enough, slicing is not necessary
        if (width <= 1200)
        {
            using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
            return await _ocrService.GetMapDetectionsAsync(stream, originalFileName);
        }

        var allDetections = new ConcurrentBag<YoloDetection>();
        var tasks = new List<Task>();
        int sliceWidth = (int)(width * 0.60); // 60% of width with overlap in the middle

        // Slice 1: Left side
        tasks.Add(ProcessSliceAsync(imagePath, new Rectangle(0, 0, sliceWidth, height), 0, originalFileName, allDetections));

        // Slice 2: Right side
        int startX = width - sliceWidth;
        tasks.Add(ProcessSliceAsync(imagePath, new Rectangle(startX, 0, sliceWidth, height), startX, originalFileName, allDetections));

        await Task.WhenAll(tasks);

        return MergeDetections(allDetections.ToList());
    }

    /// <summary>
    /// Finds city candidates based purely on text (Fallback when YOLO fails).
    /// Uses DynamicHudLocator to ignore UI text blocks.
    /// </summary>
    public List<OcrRegionCandidate> FindTextBasedCandidates(
        List<AnalyzedBlock> blocks,
        int imgWidth,
        int imgHeight,
        HashSet<string> coveredAreas)
    {
        var candidates = new List<OcrRegionCandidate>();

        // 1. DYNAMIC MASK CONSTRUCTION
        // Analyzes where the UI buttons are and creates forbidden zones (red zones)
        var hudMask = DynamicHudLocator.BuildDynamicMask(blocks, imgWidth, imgHeight);

        foreach (var block in blocks)
        {
            string text = block.Raw.Text.Trim();

            // Basic text quality filters
            if (text.Length < 3) continue;
            if (IsNoiseOrRatio(text)) continue;

            // Explicit Blocklist (forbidden words from vocabulary)
            if (RokVocabulary.MapUiBlocklist.Any(bad => text.Contains(bad, StringComparison.OrdinalIgnoreCase))) continue;

            // Geometry calculations
            var r = block.Raw.Box;
            int bx = (int)r[0][0];
            int by = (int)r[0][1];
            int bw = (int)(r[1][0] - r[0][0]);
            int bh = (int)(r[2][1] - r[1][1]);
            int cx = bx + (bw / 2);
            int cy = by + (bh / 2);

            // SPECIAL LOGIC: Cities with TAG [XXX] are very obvious and reliable.
            // If it has brackets, we give it a higher "vote of confidence".
            bool looksLikeCityStrong = text.Contains("[") && text.Contains("]");

            // 2. DYNAMIC HUD APPLICATION
            // If it's NOT a strong candidate (no tag) AND it is inside a UI zone, we ignore it.
            if (!looksLikeCityStrong && !hudMask.IsSafe(cx, cy))
            {
                continue; // Text is overlapping chat, resources, or menu. Garbage.
            }

            // Check if this area is already covered by a YOLO detection (prevent duplicates)
            var simpleBox = new int[] { bx, by, bw, bh };
            if (IsAreaCovered(simpleBox, coveredAreas)) continue;

            // If it passed all filters, it is a valid city candidate!
            candidates.Add(new OcrRegionCandidate
            {
                Id = Guid.NewGuid().ToString(),
                Box = new int[] {
                    Math.Max(0, bx - 15),
                    Math.Max(0, by - 5),
                    bw + 30,
                    bh + 10
                },
                CenterX = cx,
                CenterY = cy,
                HasShield = false,
                Source = "Text_Fallback",
                Strategy = "MapLabel"
            });
        }

        return candidates;
    }

    // =================================================================================
    // PRIVATE HELPERS
    // =================================================================================

    private bool IsNoiseOrRatio(string text)
    {
        // 1. Check strict blocklist from Vocabulary
        if (RokVocabulary.MapUiBlocklist.Any(bad => text.Contains(bad, StringComparison.OrdinalIgnoreCase))) return true;

        // 2. Check Noise Patterns (/, :) from Vocabulary
        if (RokVocabulary.MapNoiseKeywords.Any(noise => text.Contains(noise))) return true;

        // 3. Numeric check (Resource counters like 13.5M)
        if (text.Any(char.IsDigit) && (text.Contains('M') || text.Contains('K')))
        {
            // Allow names like "Player123" but block "12.5M"
            bool hasLetters = text.Count(char.IsLetter) > 1;
            if (!hasLetters) return true;
        }

        return false;
    }

    private async Task ProcessSliceAsync(string sourcePath, Rectangle cropArea, int xOffset, string fileName, ConcurrentBag<YoloDetection> results)
    {
        string slicePath = Path.Combine(Path.GetDirectoryName(sourcePath)!, $"slice_{Guid.NewGuid()}.jpg");
        try
        {
            using (var image = await Image.LoadAsync(sourcePath))
            {
                image.Mutate(x => x.Crop(cropArea));
                await image.SaveAsync(slicePath);
            }
            using (var sliceStream = new FileStream(slicePath, FileMode.Open, FileAccess.Read))
            {
                var detections = await _ocrService.GetMapDetectionsAsync(sliceStream, fileName);
                foreach (var d in detections)
                {
                    d.Box[0] += xOffset;
                    results.Add(d);
                }
            }
        }
        catch { }
        finally
        {
            if (File.Exists(slicePath)) File.Delete(slicePath);
        }
    }

    private List<YoloDetection> MergeDetections(List<YoloDetection> raw)
    {
        var final = new List<YoloDetection>();
        foreach (var det in raw.OrderByDescending(d => d.Confidence))
        {
            double cx = det.Box[0] + det.Box[2] / 2.0;
            double cy = det.Box[1] + det.Box[3] / 2.0;

            // Remove nearby duplicates caused by slicing overlap
            bool isDup = final.Any(ex =>
            {
                double exCx = ex.Box[0] + ex.Box[2] / 2.0;
                double exCy = ex.Box[1] + ex.Box[3] / 2.0;
                return ex.ClassName == det.ClassName && Math.Sqrt(Math.Pow(cx - exCx, 2) + Math.Pow(cy - exCy, 2)) < 30;
            });

            if (!isDup) final.Add(det);
        }
        return final;
    }

    private bool IsAreaCovered(int[] box, HashSet<string> covered)
    {
        int cx = box[0] + (box[2] / 2);
        int cy = box[1] + (box[3] / 2);
        // Uses a rough 50x50 pixel grid to check for overlapping areas
        return covered.Contains($"{cx / 50}_{cy / 50}");
    }

    /// <summary>
    /// Zooms in on a specific label area, upscales it, and re-runs OCR.
    /// Used when the initial reading is suspicious (e.g., missing tags).
    /// </summary>
    public async Task<string?> ZoomOnLabel(
        string imagePath,
        int[] box, // [x, y, w, h]
        OcrAnalysisContext context)
    {
        context.StartTimer("Magnifier_Zoom");
        string cropPath = "";

        try
        {
            // Expand the box slightly to catch brackets that might be on the edge
            int x = Math.Max(0, box[0] - 10);
            int y = Math.Max(0, box[1] - 5);
            int w = box[2] + 20;
            int h = box[3] + 10;

            using (var image = await Image.LoadAsync(imagePath))
            {
                // Clamp to image bounds
                w = Math.Min(w, image.Width - x);
                h = Math.Min(h, image.Height - y);

                image.Mutate(ctx =>
                {
                    // 1. Crop
                    ctx.Crop(new Rectangle(x, y, w, h));

                    // 2. Upscale (3x for better text definition)
                    ctx.Resize(w * 3, h * 3, KnownResamplers.Bicubic);

                    // 3. Enhance (Sharpen + Contrast)
                    ctx.Contrast(1.3f);
                    ctx.GaussianSharpen(1.0f);
                });

                string filename = $"magnify_{Guid.NewGuid()}.png";
                cropPath = Path.Combine(Path.GetDirectoryName(imagePath)!, filename);
                await image.SaveAsync(cropPath);
            }

            // 4. Re-analyze with Global OCR
            var (blocks, _) = await _ocrService.AnalyzeImageAsync(cropPath);

            if (blocks != null && blocks.Any())
            {
                // Join blocks horizontally to reconstruct the string
                var sortedBlocks = blocks.OrderBy(b => b.Box[0][0]).ToList();
                string combinedText = string.Join(" ", sortedBlocks.Select(b => b.Text.Trim()));

                context.StopTimer("Magnifier_Zoom");
                return combinedText;
            }

            context.StopTimer("Magnifier_Zoom");
            return null;
        }
        catch (Exception ex)
        {
            context.LogWarning("MapMagnifier", "MAGNIFIER_ERROR", $"Zoom failed: {ex.Message}", "LOW");
            context.StopTimer("Magnifier_Zoom");
            return null;
        }
        finally
        {
            if (File.Exists(cropPath)) try { File.Delete(cropPath); } catch { }
        }
    }
}
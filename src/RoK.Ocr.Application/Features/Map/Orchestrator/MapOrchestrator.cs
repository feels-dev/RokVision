using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Application.Common.Models;
using RoK.Ocr.Application.Features.Map.Neurons;
using RoK.Ocr.Application.Features.Map.Services;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Map;
using SixLabors.ImageSharp;

namespace RoK.Ocr.Application.Features.Map.Orchestrator;

/// <summary>
/// Map Orchestrator v6.0 (Clean Architecture Compliant)
/// Orchestrates the flow between Image Services, AI Detection, and OCR Neurons.
/// </summary>
public class MapOrchestrator
{
    private readonly IOcrService _ocrService;
    private readonly IImageStorage _storage;
    private readonly MapMagnifier _magnifier;
    private readonly CoordinateNeuron _coordNeuron = new();
    private readonly CityNeuron _cityNeuron = new();

    public MapOrchestrator(IOcrService ocrService, IImageStorage storage)
    {
        _ocrService = ocrService;
        _storage = storage;
        _magnifier = new MapMagnifier(ocrService, storage);
    }

    public async Task<(MapAnalysisResult Result, OcrAnalysisContext Context)> AnalyzeAsync(
        Stream imageStream,
        string fileName)
    {
        var context = new OcrAnalysisContext();
        context.StartTimer("TotalOrchestration");
        var result = new MapAnalysisResult();
        string mainImagePath = string.Empty;

        try
        {
            // 1. SAVE IMAGE & METADATA
            if (imageStream.CanSeek) imageStream.Position = 0;
            mainImagePath = await _storage.SaveImageAsync(imageStream, fileName);

            int imgWidth, imgHeight;
            using (var imgInfo = await Image.LoadAsync(mainImagePath))
            {
                imgWidth = imgInfo.Width;
                imgHeight = imgInfo.Height;
                context.DebugInfo.Image = new ImageMetaDto { Path = mainImagePath, Width = imgWidth, Height = imgHeight };
            }

            // 2. YOLO DETECTION (Delegated to Magnifier for Slicing logic)
            context.StartTimer("YOLO_Detection");
            var allDetections = await _magnifier.PerformSlicedDetectionAsync(mainImagePath, imgWidth, imgHeight, fileName);

            var cityLabels = allDetections.Where(d => d.ClassName == "city_label").ToList();
            var shields = allDetections.Where(d => d.ClassName == "shield").ToList();

            context.Log($"YOLO found {cityLabels.Count} labels and {shields.Count} shields.");
            context.StopTimer("YOLO_Detection");

            // 3. GLOBAL OCR (Coordinates)
            context.StartTimer("OCR_Coordinates");
            var (rawBlocks, fullText) = await _ocrService.AnalyzeImageAsync(mainImagePath);
            context.DebugInfo.RawText = fullText;

            var analyzedBlocks = rawBlocks.Select(b => new AnalyzedBlock { Raw = b }).ToList();
            var coordResult = _coordNeuron.Process(analyzedBlocks, new(), new());

            if (coordResult.Confidence > 0)
            {
                result.KingdomNumber = coordResult.Value.K;
                result.X = coordResult.Value.X;
                result.Y = coordResult.Value.Y;

                RegisterEvidence(context, "coordinates", $"(K:{result.KingdomNumber} X:{result.X} Y:{result.Y})",
                    coordResult.SourceBlock?.Raw.Text ?? "", coordResult.Confidence, "CoordinateNeuron", ExtractBox(coordResult.SourceBlock));
            }
            context.StopTimer("OCR_Coordinates");

            // 4. CANDIDATE GENERATION (Hybrid Logic)
            var candidates = new List<OcrRegionCandidate>();
            var usedShields = new HashSet<YoloDetection>();
            var coveredAreas = new HashSet<string>();

            // 4.1. YOLO Labels
            foreach (var label in cityLabels)
            {
                double labelCx = label.Box[0] + (label.Box[2] / 2.0);
                double labelCy = label.Box[1] + (label.Box[3] / 2.0);

                var nearbyShield = shields.FirstOrDefault(s => IsShieldAbove(s, labelCx, labelCy, label.Box[2]));
                if (nearbyShield != null) usedShields.Add(nearbyShield);

                // FIX: Use label.Box.ToArray() to convert List<int> to int[]
                candidates.Add(new OcrRegionCandidate
                {
                    Box = ExpandBox(label.Box.ToArray(), 10, 5, 20, 10),
                    HasShield = nearbyShield != null,
                    CenterX = labelCx,
                    CenterY = labelCy,
                    Source = "YOLO_Label"
                });

                // FIX: Use ToArray() for helper
                MarkAreaAsCovered(label.Box.ToArray(), coveredAreas);
            }

            // 4.2. Visual Fallback (Orphan Shields)
            var orphanShields = shields.Where(s => !usedShields.Contains(s)).ToList();
            if (orphanShields.Any())
            {
                context.Log($"Fallback: {orphanShields.Count} orphan shields.");
                foreach (var shield in orphanShields)
                {
                    int shieldBottom = shield.Box[1] + shield.Box[3];

                    int w = (int)(shield.Box[2] * 1.3);
                    int h = (int)(shield.Box[3] * 0.35);
                    int x = Math.Max(0, shield.Box[0] - (int)(shield.Box[2] * 0.15));
                    int y = shieldBottom - (int)(h * 0.65);

                    var orphanBox = new int[] { x, y, w, h };

                    candidates.Add(new OcrRegionCandidate
                    {
                        Box = orphanBox,
                        HasShield = true,
                        CenterX = shield.Box[0] + (shield.Box[2] / 2.0),
                        CenterY = shield.Box[1] + (shield.Box[3] / 2.0),
                        Source = "Fallback_Shield"
                    });

                    MarkAreaAsCovered(orphanBox, coveredAreas);
                }
            }

            // 4.3. Text Fallback (Delegated to Magnifier with Safe Canvas logic)
            var textCandidates = _magnifier.FindTextBasedCandidates(analyzedBlocks, imgWidth, imgHeight, coveredAreas);
            if (textCandidates.Any())
            {
                context.Log($"Fallback: Found {textCandidates.Count} cities via Text Analysis (Safe Canvas).");
                candidates.AddRange(textCandidates);
            }

            if (!candidates.Any())
            {
                context.LogWarning("WARN_NO_CITIES", "No cities detected via YOLO or Text Analysis.");
                return (result, context);
            }

            // 5. BATCH OCR PROCESSING
            context.StartTimer("OCR_BatchCities");

            // FIX: Explicitly map to the tuple expected by the Service
            var regionsToOcr = candidates.Select(c => (Id: c.Id, Box: c.Box, Strategy: c.Strategy)).ToList();

            var batchResults = await _ocrService.AnalyzeBatchAsync(mainImagePath, regionsToOcr);
            context.StopTimer("OCR_BatchCities");

            // Inject Crops into Debug
            if (batchResults.Any() && context.DebugInfo != null)
            {
                context.DebugInfo.RawText += "\n\n--- BATCH CROPS ---";
                foreach (var res in batchResults) if (!string.IsNullOrWhiteSpace(res.Text)) context.DebugInfo.RawText += $"\n{res.Text}";
            }

            // 6. FINAL PARSING
            context.StartTimer("CityParsing");
            int cityIndex = 0;

            foreach (var candidate in candidates)
            {
                var ocrResult = batchResults.FirstOrDefault(b => b.CustomId == candidate.Id);
                string textToParse = ocrResult?.Text ?? "";

                if (string.IsNullOrWhiteSpace(textToParse)) continue;

                var city = new MapCity();

                var parsedData = _cityNeuron.Parse(textToParse, candidate.HasShield);

                if (parsedData.Name == "--INVALID--") continue;

                bool missingTag = string.IsNullOrEmpty(parsedData.AllianceTag);

                if (missingTag)
                {

                    context.Log($"[Magnifier] Checking potential missing tag for: '{textToParse}'");


                    var refinedText = await _magnifier.ZoomOnLabel(mainImagePath, candidate.Box, context);

                    if (!string.IsNullOrWhiteSpace(refinedText))
                    {

                        var reParsed = _cityNeuron.Parse(refinedText, candidate.HasShield);


                        if (reParsed.Name != "--INVALID--" && !string.IsNullOrEmpty(reParsed.AllianceTag))
                        {
                            context.Log($"[Magnifier] SUCCESS! Refined '{textToParse}' -> '{refinedText}'");
                            parsedData = reParsed;
                            textToParse = refinedText; 
                        }
                        else
                        {
                            context.Log($"[Magnifier] No tag found after zoom. Confirmed as No-Tag.");
                        }
                    }
                }

                city.Name = parsedData.Name;
                city.AllianceTag = parsedData.AllianceTag;
                city.ScreenLocation = new ScreenLocationDto(candidate.CenterX, candidate.CenterY);
                city.HasShield = candidate.HasShield;

                result.Cities.Add(city);

                RegisterCityEvidence(context, city, textToParse, ocrResult?.Confidence ?? 0.8, candidate, cityIndex);
                cityIndex++;

                context.Log($"City: [{city.AllianceTag}] {city.Name} | Shield: {city.HasShield}");
            }
            context.StopTimer("CityParsing");
        }
        catch (Exception ex)
        {
            context.LogError($"Critical Error: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(mainImagePath) && File.Exists(mainImagePath))
                try { File.Delete(mainImagePath); } catch { }

            context.StopTimer("TotalOrchestration");
        }

        return (result, context);
    }

    // =================================================================================
    // HELPERS
    // =================================================================================

    private bool IsShieldAbove(YoloDetection s, double labelCx, double labelCy, double labelW)
    {
        double shieldCx = s.Box[0] + (s.Box[2] / 2.0);
        double shieldCy = s.Box[1] + (s.Box[3] / 2.0);
        bool isAlignedX = Math.Abs(shieldCx - labelCx) < (labelW * 1.5);
        bool isAbove = (labelCy - shieldCy) > -50 && (labelCy - shieldCy) < 250;
        return isAlignedX && isAbove;
    }

    private void MarkAreaAsCovered(int[] box, HashSet<string> covered)
    {
        int cx = box[0] + (box[2] / 2);
        int cy = box[1] + (box[3] / 2);
        covered.Add($"{cx / 50}_{cy / 50}");
    }

    private int[] ExpandBox(int[] b, int l, int t, int r, int d)
        => new int[] { Math.Max(0, b[0] - l), Math.Max(0, b[1] - t), b[2] + l + r, b[3] + t + d };

    private List<int>? ExtractBox(AnalyzedBlock? block)
    {
        if (block == null) return null;
        var r = block.Raw.Box;
        return new List<int> { (int)r[0][0], (int)r[0][1], (int)(r[1][0] - r[0][0]), (int)(r[2][1] - r[1][1]) };
    }

    private void RegisterEvidence(OcrAnalysisContext ctx, string key, object value, string raw, double conf, string method, List<int>? box)
    {
        ctx.Evidence[key] = new FieldEvidenceDto { Value = value, Raw = raw, Confidence = Math.Round(conf, 2), Method = method, Box = box };
    }

    private void RegisterCityEvidence(OcrAnalysisContext ctx, MapCity city, string raw, double conf, OcrRegionCandidate cand, int idx)
    {
        string p = $"city_{idx}";
        var b = new List<int> { cand.Box[0], cand.Box[1], cand.Box[2], cand.Box[3] };
        double c = Math.Round(conf * 100, 2);

        RegisterEvidence(ctx, $"{p}_name", city.Name, raw, c, $"CityNeuron_{cand.Source}", b);
        RegisterEvidence(ctx, $"{p}_tag", city.AllianceTag, raw, c, $"CityNeuron_{cand.Source}", b);
        RegisterEvidence(ctx, $"{p}_shield", city.HasShield, city.HasShield ? "Yes" : "No", 100, "Yolo_Or_Heuristic", b);
    }
}
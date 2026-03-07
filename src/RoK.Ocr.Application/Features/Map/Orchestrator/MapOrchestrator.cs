using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Application.Common.Models;
using RoK.Ocr.Application.Features.Map.Neurons;
using RoK.Ocr.Application.Features.Map.Services;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Map;
using SixLabors.ImageSharp;

namespace RoK.Ocr.Application.Features.Map.Orchestrator;

/// <summary>
/// Map Orchestrator v6.0 (Enterprise Architecture Compliant)
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

                // Set Image Context for Normalized Coordinates
                context.ImageWidth = imgWidth;
                context.ImageHeight = imgHeight;
                context.DebugInfo.ImagePath = mainImagePath;
            }

            // 2. YOLO DETECTION
            context.StartTimer("YOLO_Detection");
            var allDetections = await _magnifier.PerformSlicedDetectionAsync(mainImagePath, imgWidth, imgHeight, fileName);

            var cityLabels = allDetections.Where(d => d.ClassName == "city_label").ToList();
            var shields = allDetections.Where(d => d.ClassName == "shield").ToList();

            context.Log("YoloDetector", $"Found {cityLabels.Count} city labels and {shields.Count} shields.");
            context.DebugInfo.YoloMetrics["TotalDetections"] = allDetections.Count;
            context.DebugInfo.YoloMetrics["CityLabels"] = cityLabels.Count;
            context.DebugInfo.YoloMetrics["Shields"] = shields.Count;

            context.StopTimer("YOLO_Detection");

            // 3. GLOBAL OCR (Coordinates & Anchors)
            context.StartTimer("OCR_Coordinates");
            var (rawBlocks, fullText) = await _ocrService.AnalyzeImageAsync(mainImagePath);

            // Assign Global Text purely to Debug RawText
            context.DebugInfo.RawText = fullText;

            var analyzedBlocks = rawBlocks.Select(b => new AnalyzedBlock { Raw = b, CanvasWidth = imgWidth, CanvasHeight = imgHeight }).ToList();
            var coordResult = _coordNeuron.Process(analyzedBlocks, new(), new());

            if (coordResult.Confidence > 0)
            {
                result.KingdomNumber = coordResult.Value.K;
                result.X = coordResult.Value.X;
                result.Y = coordResult.Value.Y;

                // Register Coordinates Evidence
                context.RegisterResult("coordinates_k", CreateResult(result.KingdomNumber, coordResult.Confidence, coordResult.SourceBlock, coordResult.Strategy), "CoordinateNeuron");
                context.RegisterResult("coordinates_x", CreateResult(result.X, coordResult.Confidence, coordResult.SourceBlock, coordResult.Strategy), "CoordinateNeuron");
                context.RegisterResult("coordinates_y", CreateResult(result.Y, coordResult.Confidence, coordResult.SourceBlock, coordResult.Strategy), "CoordinateNeuron");
            }

            // POPULATE ANCHORS: Scan for UI elements to fill 'anchorsFound' in Debug JSON
            ExtractAndRegisterAnchors(analyzedBlocks, context);

            context.StopTimer("OCR_Coordinates");

            // 4. CANDIDATE GENERATION
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

                // Register UI Interactable for Automation (Clicking on the city label)
                var interactableBlock = new AnalyzedBlock
                {
                    Raw = new OcrBlock
                    {
                        Box = new List<List<double>>
                        {
                            new() { label.Box[0], label.Box[1] },
                            new() { label.Box[0] + label.Box[2], label.Box[1] },
                            new() { label.Box[0] + label.Box[2], label.Box[1] + label.Box[3] },
                            new() { label.Box[0], label.Box[1] + label.Box[3] }
                        }
                    }
                };
                context.RegisterInteractable($"city_label_{candidates.Count}", label.Confidence * 100, "YOLO_Map_Model", interactableBlock);

                candidates.Add(new OcrRegionCandidate
                {
                    Id = Guid.NewGuid().ToString(),
                    Box = ExpandBox(label.Box.ToArray(), 10, 5, 20, 10),
                    HasShield = nearbyShield != null,
                    CenterX = labelCx,
                    CenterY = labelCy,
                    Source = "YOLO_Label",
                    Strategy = "MapLabel"
                });

                MarkAreaAsCovered(label.Box.ToArray(), coveredAreas);
            }

            // 4.2. Text Fallback
            var textCandidates = _magnifier.FindTextBasedCandidates(analyzedBlocks, imgWidth, imgHeight, coveredAreas);
            if (textCandidates.Any())
            {
                context.Log("MapMagnifier", $"[Fallback] Found {textCandidates.Count} potential cities via Text Analysis.");
                foreach (var c in textCandidates)
                {
                    c.Id = Guid.NewGuid().ToString();
                    c.Strategy = "MapLabel_TextHeuristic";
                }
                candidates.AddRange(textCandidates);
            }

            if (!candidates.Any())
            {
                context.LogWarning("CityNeuron", "WARN_NO_CITIES", "No cities detected via YOLO or Text Analysis.", "MEDIUM");
                return (result, context);
            }

            // 5. BATCH OCR PROCESSING
            context.StartTimer("OCR_BatchCities");
            var regionsToOcr = candidates.Select(c => (Id: c.Id, Box: c.Box, Strategy: c.Strategy)).ToList();
            var batchResults = await _ocrService.AnalyzeBatchAsync(mainImagePath, regionsToOcr);
            context.StopTimer("OCR_BatchCities");

            // --- CLEAN RAW TEXT CONCATENATION ---
            if (context.DebugInfo != null && batchResults.Any())
            {
                context.DebugInfo.RawText += "\n\n--- BATCH OCR CROPS ---";
                foreach (var batchRes in batchResults)
                {
                    if (!string.IsNullOrWhiteSpace(batchRes.Text))
                    {
                        string shortId = batchRes.CustomId.Length >= 6 ? batchRes.CustomId.Substring(batchRes.CustomId.Length - 6) : batchRes.CustomId;
                        context.DebugInfo.RawText += $"\n[Crop_{shortId}] {batchRes.Text}";
                    }
                }
            }

            // 6. FINAL PARSING & NEURON VALIDATION
            context.StartTimer("CityParsing");
            int cityIndex = 0;

            foreach (var candidate in candidates)
            {
                string shortId = candidate.Id.Length >= 6 ? candidate.Id.Substring(candidate.Id.Length - 6) : candidate.Id;

                var ocrResult = batchResults.FirstOrDefault(b => b.CustomId == candidate.Id);
                string textToParse = ocrResult?.Text ?? "";

                if (string.IsNullOrWhiteSpace(textToParse)) continue;

                var parseResult = _cityNeuron.Parse(textToParse, candidate.HasShield);

                if (!parseResult.IsValid)
                {
                    context.Log("CityNeuron", $"Rejected candidate [{shortId}]. Reason: {parseResult.RejectReason}");
                    continue;
                }

                string name = parseResult.Name;
                string tag = parseResult.AllianceTag;
                bool missingTag = string.IsNullOrEmpty(tag);
                bool magnifierSuccess = false; // Flag de rastreio de correção

                if (missingTag)
                {
                    context.Log("MapMagnifier", $"Candidate [{shortId}] missing tag. Triggering Zoom...");

                    var refinedText = await _magnifier.ZoomOnLabel(mainImagePath, candidate.Box, context);

                    if (!string.IsNullOrWhiteSpace(refinedText))
                    {
                        var reParsed = _cityNeuron.Parse(refinedText, candidate.HasShield);

                        if (reParsed.IsValid && !string.IsNullOrEmpty(reParsed.AllianceTag))
                        {
                            context.Log("MapMagnifier", $"Success! Rescued tag for [{shortId}].");
                            name = reParsed.Name;
                            tag = reParsed.AllianceTag;
                            textToParse = refinedText;
                            magnifierSuccess = true; // SUCESSO DO AUTO-FIX!

                            context.DebugInfo.RawText += $"\n[Magnifier_{shortId}] {refinedText}";
                        }
                    }

                    context.RegisterMagnifierAttempt($"City_{shortId}", 1, "ZoomOnLabel", magnifierSuccess);
                }

                var city = new MapCity
                {
                    Name = name,
                    AllianceTag = tag,
                    ScreenLocation = new ScreenLocationDto(candidate.CenterX, candidate.CenterY),
                    HasShield = candidate.HasShield
                };

                result.Cities.Add(city);

                // Reconstruct a dummy block for spatial registration
                var dummyBlock = new AnalyzedBlock
                {
                    Raw = new OcrBlock
                    {
                        Text = textToParse,
                        Confidence = ocrResult?.Confidence ?? 0.8,
                        Box = new List<List<double>>
                        {
                            new() { candidate.Box[0], candidate.Box[1] },
                            new() { candidate.Box[0] + candidate.Box[2], candidate.Box[1] },
                            new() { candidate.Box[0] + candidate.Box[2], candidate.Box[1] + candidate.Box[3] },
                            new() { candidate.Box[0], candidate.Box[1] + candidate.Box[3] }
                        }
                    }
                };

                string p = $"city_{cityIndex}";

                context.RegisterResult(
                    $"{p}_name",
                    CreateResult(city.Name, dummyBlock.Raw.Confidence * 100, dummyBlock, candidate.Strategy),
                    $"CityNeuron_{candidate.Source}",
                    "PaddleOCR_v4",
                    magnifierSuccess
                );

                context.RegisterResult(
                    $"{p}_tag",
                    CreateResult(city.AllianceTag, dummyBlock.Raw.Confidence * 100, dummyBlock, candidate.Strategy),
                    $"CityNeuron_{candidate.Source}",
                    "PaddleOCR_v4",
                    magnifierSuccess
                );

                context.RegisterResult(
                    $"{p}_shield",
                    CreateResult(city.HasShield ? "Yes" : "No", 100, dummyBlock, "Yolo_Detection"),
                    "Yolo_Map_Model"
                );

                cityIndex++;
            }

            if (candidates.Count > 0 && result.Cities.Count == 0)
            {
                context.LogWarning("CityNeuron", "WARN_ALL_CANDIDATES_REJECTED", "Candidates found, but CityNeuron rejected all of them. Check debug logs.", "HIGH");
            }

            context.StopTimer("CityParsing");
        }
        catch (Exception ex)
        {
            context.LogError("MapOrchestrator", $"Critical Error: {ex.Message}");
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

    private void ExtractAndRegisterAnchors(List<AnalyzedBlock> blocks, OcrAnalysisContext context)
    {
        var foundAnchors = new HashSet<string>();
        var uiKeywords = RokVocabulary.TopUiAnchors.Concat(RokVocabulary.BottomUiAnchors).ToList();

        foreach (var block in blocks)
        {
            string text = block.Raw.Text.Trim();
            var matchedKeyword = uiKeywords.FirstOrDefault(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));

            if (matchedKeyword != null) foundAnchors.Add(matchedKeyword);
            else if (text.Contains("UTC", StringComparison.OrdinalIgnoreCase)) foundAnchors.Add("UTC");
            else if (text.Contains("VIP", StringComparison.OrdinalIgnoreCase)) foundAnchors.Add("VIP");
        }

        context.RegisterAnchors(foundAnchors);
    }

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

    private ExtractionResult<T> CreateResult<T>(T val, double conf, AnalyzedBlock? block, string strategy)
    {
        return new ExtractionResult<T> { Value = val, Confidence = conf, SourceBlock = block, Strategy = strategy };
    }
}
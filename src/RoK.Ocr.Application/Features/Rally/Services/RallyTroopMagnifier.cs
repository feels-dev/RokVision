using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RoK.Ocr.Application.Common.Models;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Rally;

namespace RoK.Ocr.Application.Features.Rally.Services;

public class RallyTroopMagnifier
{
    private readonly IOcrService _ocrService;
    private readonly IImageStorage _storage;

    public RallyTroopMagnifier(IOcrService ocrService, IImageStorage storage)
    {
        _ocrService = ocrService;
        _storage = storage;
    }

    /// <summary>
    /// Locates troop numbers in each participant's row, crops the adjacent left icon, 
    /// and identifies the tier color to populate the participant's troop details list.
    /// </summary>
    public async Task EnrichTroopDetailsAsync(string imagePath, List<RallyParticipant> participants, List<AnalyzedBlock> allBlocks, OcrAnalysisContext context)
    {
        if (!allBlocks.Any()) return;

        context.StartTimer("Magnifier_EnrichTroops");
        context.Log("RallyTroopMagnifier", "Starting troop color/tier enrichment via Batch OCR...");

        int imgW = (int)allBlocks.First().CanvasWidth;
        int imgH = (int)allBlocks.First().CanvasHeight;

        var cropMap = new Dictionary<string, long>();
        var cropsRequest = new List<(string Id, int[] Box, string Strategy)>();

        foreach (var p in participants)
        {
            var anchorBlock = allBlocks.FirstOrDefault(b =>
                b.Raw.Box[0][0] / (double)imgW < 0.45 &&
                (b.Raw.Box[0][1] / (double)imgH) > 0.35 &&
                ((p.Name != "--" && FuzzySharp.Fuzz.PartialRatio(b.Raw.Text, p.Name) > 85) ||
                  (p.PrimaryCommander != null && FuzzySharp.Fuzz.PartialRatio(b.Raw.Text, p.PrimaryCommander.CanonicalName) > 85))
            );

            if (anchorBlock == null) continue;

            double pY = anchorBlock.Raw.Box[0][1] / (double)imgH;

            var rawTroopBlocks = allBlocks.Where(b =>
                b.Raw.Box[0][0] / (double)imgW < 0.65 &&
                (b.Raw.Box[0][1] / (double)imgH) >= pY + 0.02 &&
                (b.Raw.Box[0][1] / (double)imgH) <= pY + 0.25 &&
                ParseTroopNumber(b.Raw.Text) > 0 &&
                !b.Raw.Text.Contains("Unidades", StringComparison.OrdinalIgnoreCase) &&
                !b.Raw.Text.Contains("Units", StringComparison.OrdinalIgnoreCase) &&
                !Regex.IsMatch(b.Raw.Text, @"(Nv\.|Lvl|Level)", RegexOptions.IgnoreCase)
            ).ToList();

            var uniqueTroops = DeduplicateBlocks(rawTroopBlocks);

            foreach (var countBlock in uniqueTroops)
            {
                // SAFETY LOCK: Text Sensor Heuristic
                // Detects if the word "Units" (or equivalent) is immediately to the left of the number.
                bool hasTextLabel = allBlocks.Any(label =>
                    label.Raw.Box[2][0] < countBlock.Raw.Box[0][0] && // Located to the left
                    label.Raw.Box[2][0] > countBlock.Raw.Box[0][0] - 300 && // Within proximity (300px)
                    Math.Abs(label.NormalizedCenter.Y - countBlock.NormalizedCenter.Y) < 0.05 && // On the same line
                    (label.Raw.Text.Contains("Unidades", StringComparison.OrdinalIgnoreCase) ||
                     label.Raw.Text.Contains("Units", StringComparison.OrdinalIgnoreCase))
                );

                if (hasTextLabel)
                {
                    // If the "Units" label is present, the game has hidden the Tier icon.
                    // Skips cropping to prevent false positive color detections (e.g., reading a Commander portrait as a T5 shield).
                    continue;
                }

                // Troop Layout confirmed. Proceeding with robust icon extraction.
                long quantity = ParseTroopNumber(countBlock.Raw.Text);
                var b = countBlock.Raw.Box;

                // Uses double precision for base geometric calculations
                double textHeight = b[2][1] - b[0][1];

                // 1. SHIELD SIZE CALCULATION
                // In-game icons are significantly larger than the accompanying text height.
                // Using a ~2.2x multiplier ensures the entire shield is framed perfectly, 
                // crucial for preserving the outer tier-color borders (Purple T4, Gold T5) during CV processing.
                int shieldSize = (int)(textHeight * 2.2);

                // 2. X-AXIS POSITIONING (Horizontal Alignment)
                // The shield is rendered tightly against the number; gap is set to 25% of the text height.
                int gap = (int)(textHeight * 0.25);
                int textLeftX = (int)b[0][0];

                int cropX = Math.Max(0, textLeftX - gap - shieldSize);

                // 3. Y-AXIS POSITIONING (Vertical Alignment)
                double textCenterY = b[0][1] + (textHeight / 2.0);
                int cropY = Math.Max(0, (int)(textCenterY - (shieldSize / 2.0)));

                string cropId = $"icon_{p.Name}_{quantity}_{Guid.NewGuid().ToString().Substring(0, 4)}";

                // Maps and queues the crop request for batch OCR processing
                cropMap[cropId] = quantity;
                cropsRequest.Add((cropId, new int[] { cropX, cropY, shieldSize, shieldSize }, "TroopColor"));
            }
        }

        if (cropsRequest.Count == 0)
        {
            context.StopTimer("Magnifier_EnrichTroops");
            return;
        }

        context.Log("RallyTroopMagnifier", $"Queued {cropsRequest.Count} crops for Troop Tier Color detection.");

        var results = await _ocrService.AnalyzeBatchAsync(imagePath, cropsRequest);

        foreach (var res in results)
        {
            var parts = res.CustomId.Split('_');
            if (parts.Length >= 2)
            {
                string pName = parts[1];
                var targetP = participants.FirstOrDefault(p => p.Name == pName);

                if (targetP != null && cropMap.TryGetValue(res.CustomId, out long qty))
                {
                    var detail = MapColorToTroopDetail(res.Text);
                    detail.Count = qty;
                    targetP.TroopDetails.Add(detail);
                }
            }
        }

        context.ExecutionTrace.MagnifierUsed = true;
        context.StopTimer("Magnifier_EnrichTroops");
        context.Log("RallyTroopMagnifier", "Troop color enrichment completed successfully.");
    }

    /// <summary>
    /// Filters physically overlapping blocks, retaining only the one with the highest confidence. 
    /// Resolves "Ghost Troop" duplication issues.
    /// </summary>
    private List<AnalyzedBlock> DeduplicateBlocks(List<AnalyzedBlock> blocks)
    {
        var result = new List<AnalyzedBlock>();
        var sorted = blocks.OrderByDescending(b => b.Raw.Confidence).ToList();

        foreach (var candidate in sorted)
        {
            bool isDuplicate = result.Any(existing =>
                Math.Abs(existing.Raw.Box[0][0] - candidate.Raw.Box[0][0]) < 20 &&
                Math.Abs(existing.Raw.Box[0][1] - candidate.Raw.Box[0][1]) < 20
            );

            if (!isDuplicate) result.Add(candidate);
        }
        return result;
    }

    /// <summary>
    /// Geometric Repair Protocol. If participant name or total troop count is missing, 
    /// extrapolates their expected coordinates based on the anchor's (Commander Level) position.
    /// </summary>
    public async Task<List<OcrBlock>> RescanMissingDataAsync(string imagePath, List<AnalyzedBlock> anchorsNeedingRepair, OcrAnalysisContext context, int imgW, int imgH)
    {
        var batchRequests = new List<(string Id, int[] Box, string Strategy)>();

        foreach (var anchor in anchorsNeedingRepair)
        {
            int anchorPixelY = (int)(anchor.Raw.Box[0][1]); // Absolute Y of the anchor (e.g., Lvl. XX)

            // NAME SEARCH: Always left-aligned, located slightly ABOVE the anchor.
            int nameX = (int)(imgW * 0.08);
            int nameY = anchorPixelY - (int)(imgH * 0.09);
            int nameW = (int)(imgW * 0.35);
            int nameH = (int)(imgH * 0.08);

            nameY = Math.Max(0, nameY);
            batchRequests.Add(($"RepairName_{Guid.NewGuid()}", new[] { nameX, nameY, nameW, nameH }, "HighContrastBinary"));

            // TOTAL UNITS SEARCH: Always right-aligned.
            int unitX = (int)(imgW * 0.45);
            int unitY = anchorPixelY - (int)(imgH * 0.08);
            int unitW = (int)(imgW * 0.45);
            int unitH = (int)(imgH * 0.09);

            unitY = Math.Max(0, unitY);
            batchRequests.Add(($"RepairUnit_{Guid.NewGuid()}", new[] { unitX, unitY, unitW, unitH }, "Sharpen"));
        }

        if (!batchRequests.Any()) return new List<OcrBlock>();

        // ENENTERPRISE UPDATE: Added component name to log
        context.Log("RallyTroopMagnifier", $"Dispatching {batchRequests.Count} geometric repair boxes.");
        var results = await _ocrService.AnalyzeBatchAsync(imagePath, batchRequests);

        var globalBlocks = new List<OcrBlock>();

        foreach (var res in results)
        {
            if (res.Confidence < 0.60 || res.Box == null || res.Box.Count != 4 || res.Box[0].Count != 2) continue;

            var originalRequest = batchRequests.First(r => r.Id == res.CustomId);
            int cropStartX = originalRequest.Box[0];
            int cropStartY = originalRequest.Box[1];

            // Translates local crop coordinates back to the global image coordinate space
            var translatedBox = new List<List<double>>
            {
                new List<double> { res.Box[0][0] + cropStartX, res.Box[0][1] + cropStartY },
                new List<double> { res.Box[1][0] + cropStartX, res.Box[1][1] + cropStartY },
                new List<double> { res.Box[2][0] + cropStartX, res.Box[2][1] + cropStartY },
                new List<double> { res.Box[3][0] + cropStartX, res.Box[3][1] + cropStartY }
            };

            globalBlocks.Add(new OcrBlock
            {
                Text = res.Text,
                Confidence = res.Confidence,
                Box = translatedBox,
                CustomId = res.CustomId
            });
        }

        return globalBlocks;
    }

    private long ParseTroopNumber(string text) => long.TryParse(Regex.Replace(text, @"[^\d]", ""), out long val) ? val : 0;

    private RallyTroopDetail MapColorToTroopDetail(string detectedColor)
    {
        // Type initializes as "Unknown" since only color is parsed currently. 
        // Future implementations could utilize Shape Matching for precise unit type detection.
        var detail = new RallyTroopDetail { DetectedColor = detectedColor, Type = "Unknown" };
        switch (detectedColor.Trim())
        {
            case "Green": detail.Tier = "T2"; break;
            case "Blue": detail.Tier = "T3"; break;
            case "Purple": detail.Tier = "T4"; break;
            case "Gold": detail.Tier = "T5"; break;
            case "Red": detail.Tier = "T1"; break;
            default: detail.Tier = "Unknown"; break;
        }
        return detail;
    }
}
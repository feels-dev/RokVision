using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FuzzySharp;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Application.Common.Models;
using RoK.Ocr.Application.Features.Rally.Neurons;
using RoK.Ocr.Application.Features.Rally.Services;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Rally;

namespace RoK.Ocr.Application.Features.Rally.Orchestrator;

public enum RallyScreenContext
{
    Unknown,
    SingleDetails, // "War Details" screen (Individual Rally)
    MultiList      // General "War" screen (Multiple Rallies)
}

public class RallyOrchestrator
{
    private readonly IOcrService _ocrService;
    private readonly IVocabularyLoader _vocabLoader;
    private readonly RallyTroopMagnifier _magnifier;
    private readonly ILogger<RallyOrchestrator> _logger;

    private readonly RallyHeaderNeuron _headerNeuron;
    private readonly RallyTargetNeuron _targetNeuron;
    private readonly RallyTroopSummaryNeuron _summaryNeuron;
    private readonly RallyParticipantNeuron _participantNeuron;

    public RallyOrchestrator(
        IOcrService ocrService,
        IVocabularyLoader vocabLoader,
        RallyTroopMagnifier magnifier,
        ILogger<RallyOrchestrator> logger)
    {
        _ocrService = ocrService;
        _vocabLoader = vocabLoader;
        _magnifier = magnifier;
        _logger = logger;

        _headerNeuron = new RallyHeaderNeuron();
        _targetNeuron = new RallyTargetNeuron(_vocabLoader.GetNpcs());
        _summaryNeuron = new RallyTroopSummaryNeuron();
        _participantNeuron = new RallyParticipantNeuron(_vocabLoader);
    }

    private double GetYRatio(AnalyzedBlock b, int h) => b.Raw.Box[0][1] / (double)h;

    public async Task<(RallyResult Result, OcrAnalysisContext Context)> AnalyzeAsync(List<string> imagePaths)
    {
        var context = new OcrAnalysisContext();
        context.StartTimer("TotalRallyOrchestration");

        var result = new RallyResult();
        var processedNames = new HashSet<string>();

        // This set accumulates every block used in the final extraction to allow reverse-lookup of confidence
        var globalUsedBlocks = new HashSet<AnalyzedBlock>();

        try
        {
            for (int i = 0; i < imagePaths.Count; i++)
            {
                string path = imagePaths[i];
                bool isFirstImage = (i == 0);

                context.StartTimer($"Image_{i}_Total");
                context.Log("RallyOrchestrator", $"Processing Image {i + 1}/{imagePaths.Count}: {Path.GetFileName(path)}");

                context.StartTimer($"Image_{i}_Python");
                var (rawBlocks, _) = await _ocrService.AnalyzeImageAsync(path);
                context.StopTimer($"Image_{i}_Python");

                int imgW = 1600, imgH = 900;
                using (var imgInfo = await SixLabors.ImageSharp.Image.LoadAsync(path))
                {
                    imgW = imgInfo.Width;
                    imgH = imgInfo.Height;
                }

                if (isFirstImage)
                {
                    // Propagates image dimensions to context for Global Normalized Coordinates processing
                    context.ImageWidth = imgW;
                    context.ImageHeight = imgH;

                    // FIX: Updated to match the new DebugInformationDto structure
                    // We no longer use 'ImageMetaDto' inside DebugInfo, just the simple Path string.
                    if (string.IsNullOrEmpty(context.DebugInfo.ImagePath))
                        context.DebugInfo.ImagePath = path;
                }

                var analyzedBlocks = rawBlocks.Select(b => new AnalyzedBlock { Raw = b, CanvasWidth = imgW, CanvasHeight = imgH }).ToList();
                var usedBlocksInFrame = new HashSet<AnalyzedBlock>();

                context.StartTimer($"Image_{i}_Slicing");

                // 1. CONTEXT ANALYSIS
                RallyScreenContext screenContext = RallyScreenContext.Unknown;
                var listTitleAnchor = analyzedBlocks.FirstOrDefault(b =>
                    RallyVocabulary.TroopDetailsHeaders.Any(h => Fuzz.PartialRatio(b.Raw.Text.ToLower(), h.ToLower()) > 80));

                var isMultiListScreen = analyzedBlocks.Any(b => b.Raw.Text.Contains("Mais Recente", StringComparison.OrdinalIgnoreCase)) ||
                                        analyzedBlocks.Count(b => b.Raw.Text.Contains("Forte Bárbaro", StringComparison.OrdinalIgnoreCase)) > 1;

                if (listTitleAnchor != null) screenContext = RallyScreenContext.SingleDetails;
                else if (isMultiListScreen) screenContext = RallyScreenContext.MultiList;
                else screenContext = RallyScreenContext.SingleDetails;

                context.Log("RallyOrchestrator", $"Screen Context Identified: {screenContext}");

                // 2. DYNAMIC BOUNDARY DEFINITION
                double titleY;
                double listStartY;

                if (screenContext == RallyScreenContext.SingleDetails)
                {
                    titleY = listTitleAnchor != null ? GetYRatio(listTitleAnchor, imgH) : 0.40;
                    var firstParticipantAnchor = analyzedBlocks.FirstOrDefault(b =>
                        b.Raw.Box[0][0] / (double)imgW < 0.50 &&
                        Regex.IsMatch(b.Raw.Text, @"(Nv\.|Lvl|Level)\s*\d+", RegexOptions.IgnoreCase) &&
                        GetYRatio(b, imgH) > titleY);

                    listStartY = firstParticipantAnchor != null ? GetYRatio(firstParticipantAnchor, imgH) : 0.60;
                }
                else
                {
                    var secondRallyAnchor = analyzedBlocks
                        .Where(b => Regex.IsMatch(b.Raw.Text, @"(Nv\.|Lvl|Level)\s*\d+", RegexOptions.IgnoreCase))
                        .OrderBy(b => b.Raw.Box[0][1])
                        .Skip(1).FirstOrDefault();

                    titleY = secondRallyAnchor != null ? GetYRatio(secondRallyAnchor, imgH) - 0.05 : 1.0;
                    listStartY = 1.0;
                }

                if (isFirstImage)
                {
                    _headerNeuron.Extract(analyzedBlocks, result, usedBlocksInFrame, titleY, imgW, imgH);
                    _targetNeuron.Extract(analyzedBlocks, result, usedBlocksInFrame, titleY, imgW, imgH);

                    if (screenContext == RallyScreenContext.SingleDetails)
                    {
                        _summaryNeuron.Extract(analyzedBlocks, result, usedBlocksInFrame, titleY, listStartY, imgW, imgH);
                    }

                    result.RallyId = $"X{result.Leader.X}Y{result.Leader.Y}_X{result.Target.X}Y{result.Target.Y}";
                }
                context.StopTimer($"Image_{i}_Slicing");

                // Accumulate used blocks for confidence calculation later
                foreach (var b in usedBlocksInFrame) globalUsedBlocks.Add(b);

                // 3. PARTICIPANT EXTRACTION
                if (screenContext == RallyScreenContext.SingleDetails)
                {
                    int attempts = 0;
                    bool keepTrying = true;
                    List<RallyParticipant> participants = new();

                    while (keepTrying && attempts < 2)
                    {
                        context.StartTimer($"Image_{i}_Cycle_{attempts}");
                        var loopGraph = new TopologyGraph(analyzedBlocks, imgW, imgH);
                        double participantSearchY = listStartY - 0.15;
                        var (extractedParticipants, anchors) = _participantNeuron.ExtractParticipants(loopGraph, usedBlocksInFrame, participantSearchY);

                        participants = extractedParticipants;
                        var defectiveParticipants = participants.Where(p => p.Name == "--" || p.TotalUnits == 0).ToList();

                        if (defectiveParticipants.Any() && attempts < 1)
                        {
                            var anchorsToRepair = anchors.Where(a => defectiveParticipants.Any(dp => dp.PrimaryCommander?.CanonicalName == a.CommanderName)).Select(a => a.Node).ToList();

                            context.StartTimer($"Image_{i}_Magnifier_Scan");
                            var recoveredBlocks = await _magnifier.RescanMissingDataAsync(path, anchorsToRepair, context, imgW, imgH);
                            context.StopTimer($"Image_{i}_Magnifier_Scan");

                            var validBlocks = recoveredBlocks.Where(b => b.Box != null && b.Box.Count == 4 && b.Box[0].Count == 2).ToList();

                            foreach (var def in defectiveParticipants)
                            {
                                context.RegisterMagnifierAttempt($"Participant_{def.PrimaryCommander?.CanonicalName ?? "Unknown"}", 1, validBlocks.Any() ? "GeometricCrop_Success" : "GeometricCrop_Failed", validBlocks.Any());
                            }

                            if (validBlocks.Any())
                            {
                                analyzedBlocks.AddRange(validBlocks.Select(b => new AnalyzedBlock { Raw = b, CanvasWidth = imgW, CanvasHeight = imgH }));
                                attempts++;
                                context.StopTimer($"Image_{i}_Cycle_{attempts - 1}");
                                continue;
                            }
                        }
                        keepTrying = false;
                        context.StopTimer($"Image_{i}_Cycle_{attempts}");
                    }

                    context.StartTimer($"Image_{i}_Merge");
                    if (participants.Any()) await _magnifier.EnrichTroopDetailsAsync(path, participants, analyzedBlocks, context);

                    foreach (var p in participants)
                    {
                        if (string.IsNullOrWhiteSpace(p.Name) || p.Name == "--") continue;

                        if (Fuzz.PartialRatio(p.Name, result.Leader.Name) > 85 || p.Name.EndsWith(result.Leader.Name))
                        {
                            p.Name = result.Leader.Name;
                            p.IsLeader = true;
                        }

                        if (!processedNames.Contains(p.Name))
                        {
                            result.Participants.Add(p);
                            processedNames.Add(p.Name);
                        }
                    }
                    context.StopTimer($"Image_{i}_Merge");

                    // Accumulate participant blocks
                    foreach (var b in usedBlocksInFrame) globalUsedBlocks.Add(b);
                }

                context.StopTimer($"Image_{i}_Total");
            }

            InferTroopTypes(result, context);

            // Pass global blocks for accurate confidence calculation
            AuditRally(result, context, globalUsedBlocks);

            // 4. REGISTER RESULTS WITH REAL CONFIDENCE AND SPATIAL TRACEABILITY
            var targetEv = FindBlockEvidence(result.Target.Name, globalUsedBlocks);
            var leaderEv = FindBlockEvidence(result.Leader.Name, globalUsedBlocks);
            var capEv = FindBlockEvidence(result.Status.CurrentCapacity.ToString(), globalUsedBlocks);

            // Rally ID doesn't have a spatial block (it's generated), so null is acceptable here.
            context.RegisterResult("rally_id", CreateResult(result.RallyId, "String_Concat", 100.0, null), "RallyOrchestrator");

            // Registering with SourceBlock ensures the API outputs the 'spatial' coordinates
            context.RegisterResult("target_name", CreateResult(result.Target.Name, result.Target.IsNpc ? "Target_NpcRules" : "Target_PvPRules", targetEv.Confidence, targetEv.SourceBlock), "RallyTargetNeuron");

            context.RegisterResult("leader_name", CreateResult(result.Leader.Name, "Header_TagRegex", leaderEv.Confidence, leaderEv.SourceBlock), "RallyHeaderNeuron");

            context.RegisterResult("current_capacity", CreateResult(result.Status.CurrentCapacity, "Header_CapacityRegex", capEv.Confidence, capEv.SourceBlock), "RallyHeaderNeuron");
        }
        catch (Exception ex)
        {
            context.LogError("RallyOrchestrator", $"Critical Error in RallyOrchestrator: {ex.Message}\n{ex.StackTrace}");
            _logger.LogError(ex, "Rally Analysis Failed");
        }
        finally
        {
            context.StopTimer("TotalRallyOrchestration");
        }

        return (result, context);
    }

    /// <summary>
    /// Lookups both the real OCR confidence and the original spatial block that provided the text.
    /// Uses Fuzzy matching to handle slight cleanup changes.
    /// </summary>
    private (double Confidence, AnalyzedBlock? SourceBlock) FindBlockEvidence(string value, HashSet<AnalyzedBlock> blocks)
    {
        if (string.IsNullOrWhiteSpace(value)) return (0, null);

        var exact = blocks.FirstOrDefault(b => b.Raw.Text.Contains(value));
        if (exact != null) return (exact.Raw.Confidence * 100, exact);


        var bestFuzzy = blocks
            .Select(b => new { Block = b, Ratio = Fuzz.PartialRatio(b.Raw.Text, value) })
            .Where(x => x.Ratio > 85)
            .OrderByDescending(x => x.Ratio)
            .FirstOrDefault();

        if (bestFuzzy != null) return (bestFuzzy.Block.Raw.Confidence * 100, bestFuzzy.Block);

        if (long.TryParse(value, out _))
        {
            var numberMatch = blocks
                .Select(b => new { Block = b, CleanText = Regex.Replace(b.Raw.Text, @"[^\d]", "") })
                .Where(x => x.CleanText.Contains(value)) // Procura "206000" dentro de "2060002200000"
                .OrderByDescending(x => x.Block.Raw.Confidence)
                .FirstOrDefault();

            if (numberMatch != null) return (numberMatch.Block.Raw.Confidence * 100, numberMatch.Block);
        }

        if (value == "Barbarian Fort")
        {
            var fortMatch = blocks
                .Where(b => b.Raw.Text.Contains("Forte") || b.Raw.Text.Contains("Fort"))
                .OrderByDescending(b => b.Raw.Confidence)
                .FirstOrDefault();
            if (fortMatch != null) return (fortMatch.Raw.Confidence * 100, fortMatch);
        }

        return (50.0, null);
    }

    private void InferTroopTypes(RallyResult result, OcrAnalysisContext context)
    {
        var globalTotals = new Dictionary<string, long>
        {
            { "Infantry", result.GlobalTroops.Infantry },
            { "Cavalry", result.GlobalTroops.Cavalry },
            { "Archer", result.GlobalTroops.Archer },
            { "Siege", result.GlobalTroops.Siege }
        };

        var activeTroops = globalTotals.Where(t => t.Value > 0).ToList();
        if (!activeTroops.Any()) return;

        foreach (var p in result.Participants)
        {
            foreach (var detail in p.TroopDetails)
            {
                if (detail.Type != "Unknown") continue;

                if (activeTroops.Count == 1)
                {
                    detail.Type = activeTroops.First().Key;
                    context.Log("TroopInferenceEngine", $"Logical Inference: Set {p.Name}'s {detail.Count} troops to {detail.Type} (Single-troop rally).");
                    continue;
                }

                var exactMatch = activeTroops.Where(t => t.Value == detail.Count).ToList();
                if (exactMatch.Count == 1)
                {
                    detail.Type = exactMatch.First().Key;
                    context.Log("TroopInferenceEngine", $"Logical Inference: Set {p.Name}'s {detail.Count} troops to {detail.Type} (Exact amount match).");
                    continue;
                }

                var mathematicallyPossible = activeTroops.Where(t => t.Value >= detail.Count).ToList();
                if (mathematicallyPossible.Count == 1)
                {
                    detail.Type = mathematicallyPossible.First().Key;
                    context.Log("TroopInferenceEngine", $"Logical Inference: Set {p.Name}'s {detail.Count} troops to {detail.Type} (Logical elimination).");
                }
            }
        }
    }

    /// <summary>
    /// Helper to create a Result with dynamic confidence and spatial traceability.
    /// </summary>
    private ExtractionResult<T> CreateResult<T>(T val, string strategy, double confidence, AnalyzedBlock? sourceBlock) => new ExtractionResult<T>
    {
        Value = val,
        Confidence = Math.Clamp(confidence, 0, 100),
        Strategy = strategy,
        SourceBlock = sourceBlock
    };

    /// <summary>
    /// Lookups the real OCR confidence from the block that provided the text.
    /// Uses Fuzzy matching to handle slight cleanup changes (like removed brackets).
    /// </summary>
    private double FindBlockConfidence(string value, HashSet<AnalyzedBlock> blocks)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        // Try exact match first
        var exact = blocks.FirstOrDefault(b => b.Raw.Text.Contains(value));
        if (exact != null) return exact.Raw.Confidence * 100;

        // Try fuzzy match
        var bestFuzzy = blocks
            .Select(b => new { Block = b, Ratio = Fuzz.PartialRatio(b.Raw.Text, value) })
            .Where(x => x.Ratio > 85)
            .OrderByDescending(x => x.Ratio)
            .FirstOrDefault();

        return bestFuzzy != null ? bestFuzzy.Block.Raw.Confidence * 100 : 50.0; // Fallback to 50% if logic found it but block is lost
    }

    private void AuditRally(RallyResult result, OcrAnalysisContext context, HashSet<AnalyzedBlock> usedBlocks)
    {
        // 1. Calculate the Base Confidence from the OCR data directly
        // Average confidence of all blocks used in the process (Header + Participants + Target)
        double baseConfidence = usedBlocks.Any() ? usedBlocks.Average(b => b.Raw.Confidence) * 100 : 0;

        // If no blocks, something is wrong
        if (baseConfidence == 0)
        {
            result.OverallConfidence = 0;
            return;
        }

        // 2. Apply Business Logic Validation
        // Audits the aggregate participant troops against the header's reported capacity
        if (result.Participants.Any())
        {
            long sumFromList = result.Participants.Sum(p => p.TotalUnits);
            long headerCurrent = result.Status.CurrentCapacity;

            if (headerCurrent > 0)
            {
                long diff = Math.Abs(headerCurrent - sumFromList);
                double errorRate = (double)diff / headerCurrent;

                if (errorRate < 0.05)
                {
                    // Bonus for mathematical perfection (+5%)
                    baseConfidence = Math.Min(100, baseConfidence * 1.05);
                    context.Log("ConsistencyAuditor", "Audit Passed: Participant sum matches Header capacity.");
                }
                else
                {
                    // Penalty proportional to error rate
                    // e.g., 10% error reduces confidence by ~15%
                    double penaltyMultiplier = 1.0 - (errorRate * 1.5);
                    baseConfidence = Math.Max(40, baseConfidence * penaltyMultiplier);

                    string warning = $"Capacity Mismatch: Header says {headerCurrent}, List sums to {sumFromList} (Diff: {diff})";
                    result.Warnings.Add(warning);
                    context.LogWarning("ConsistencyAuditor", "WARN_CAPACITY_MISMATCH", warning, "HIGH", "currentCapacity");
                }
            }
            else
            {
                // If header capacity couldn't be read, trust the list sum but apply small penalty
                result.Status.CurrentCapacity = sumFromList;
                baseConfidence *= 0.90;
            }
        }
        else
        {
            // MultiList screens (no participants) shouldn't be penalized if the header is clear
            if (string.IsNullOrEmpty(result.Target.Name)) baseConfidence -= 20;
        }

        if (string.IsNullOrEmpty(result.RallyId) || result.RallyId.Contains("X0Y0")) baseConfidence -= 15;

        result.OverallConfidence = Math.Round(Math.Clamp(baseConfidence, 0, 100), 2);
    }
}
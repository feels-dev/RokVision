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

        try
        {
            for (int i = 0; i < imagePaths.Count; i++)
            {
                string path = imagePaths[i];
                bool isFirstImage = (i == 0);

                context.StartTimer($"Image_{i}_Total");
                context.Log($"Processing Image {i + 1}/{imagePaths.Count}: {Path.GetFileName(path)}");

                context.StartTimer($"Image_{i}_Python");
                var (rawBlocks, _) = await _ocrService.AnalyzeImageAsync(path);
                context.StopTimer($"Image_{i}_Python");

                int imgW = 1600, imgH = 900;
                using (var imgInfo = await SixLabors.ImageSharp.Image.LoadAsync(path))
                {
                    imgW = imgInfo.Width;
                    imgH = imgInfo.Height;
                    if (context.DebugInfo.Image == null) context.DebugInfo.Image = new ImageMetaDto { Path = path, Width = imgW, Height = imgH };
                }

                var analyzedBlocks = rawBlocks.Select(b => new AnalyzedBlock { Raw = b, CanvasWidth = imgW, CanvasHeight = imgH }).ToList();
                var usedBlocks = new HashSet<AnalyzedBlock>();

                context.StartTimer($"Image_{i}_Slicing");

                // 1. CONTEXT ANALYSIS: Determine the current UI screen
                RallyScreenContext screenContext = RallyScreenContext.Unknown;

                var listTitleAnchor = analyzedBlocks.FirstOrDefault(b =>
                    RallyVocabulary.TroopDetailsHeaders.Any(h => Fuzz.PartialRatio(b.Raw.Text.ToLower(), h.ToLower()) > 80));

                var isMultiListScreen = analyzedBlocks.Any(b => b.Raw.Text.Contains("Mais Recente", StringComparison.OrdinalIgnoreCase)) ||
                                        analyzedBlocks.Count(b => b.Raw.Text.Contains("Forte Bárbaro", StringComparison.OrdinalIgnoreCase)) > 1;

                if (listTitleAnchor != null)
                    screenContext = RallyScreenContext.SingleDetails;
                else if (isMultiListScreen)
                    screenContext = RallyScreenContext.MultiList;
                else
                    screenContext = RallyScreenContext.SingleDetails; // Fallback to Single Details

                context.Log($"Screen Context Identified: {screenContext}");

                // 2. CONTEXT-AWARE DYNAMIC BOUNDARY DEFINITION
                double titleY;
                double listStartY;

                if (screenContext == RallyScreenContext.SingleDetails)
                {
                    // Standard behavior for the Details screen
                    titleY = listTitleAnchor != null ? GetYRatio(listTitleAnchor, imgH) : 0.40;

                    var firstParticipantAnchor = analyzedBlocks.FirstOrDefault(b =>
                        b.Raw.Box[0][0] / (double)imgW < 0.50 &&
                        Regex.IsMatch(b.Raw.Text, @"(Nv\.|Lvl|Level)\s*\d+", RegexOptions.IgnoreCase) &&
                        GetYRatio(b, imgH) > titleY); // Ensures the participant anchor is positioned below the global troop summary

                    listStartY = firstParticipantAnchor != null ? GetYRatio(firstParticipantAnchor, imgH) : 0.60;
                }
                else
                {
                    // Multi-Rally screen: No global summary or participant list available.
                    // Isolates the topmost Rally card.
                    var secondRallyAnchor = analyzedBlocks
                        .Where(b => Regex.IsMatch(b.Raw.Text, @"(Nv\.|Lvl|Level)\s*\d+", RegexOptions.IgnoreCase))
                        .OrderBy(b => b.Raw.Box[0][1]) // Orders strictly from top to bottom
                        .Skip(1).FirstOrDefault(); // Identifies the second target anchor to define the bottom boundary of the first card

                    titleY = secondRallyAnchor != null ? GetYRatio(secondRallyAnchor, imgH) - 0.05 : 1.0;
                    listStartY = 1.0; // Bypasses participant extraction
                }

                if (isFirstImage)
                {
                    // Header and Target nodes are located ABOVE the defined Y-axis threshold
                    _headerNeuron.Extract(analyzedBlocks, result, usedBlocks, titleY, imgW, imgH);
                    _targetNeuron.Extract(analyzedBlocks, result, usedBlocks, titleY, imgW, imgH);

                    if (screenContext == RallyScreenContext.SingleDetails)
                    {
                        // Troop summary is exclusive to the Single Details screen
                        _summaryNeuron.Extract(analyzedBlocks, result, usedBlocks, titleY, listStartY, imgW, imgH);
                    }

                    result.RallyId = $"X{result.Leader.X}Y{result.Leader.Y}_X{result.Target.X}Y{result.Target.Y}";
                }
                context.StopTimer($"Image_{i}_Slicing");

                // 3. PARTICIPANT EXTRACTION (Single Details context only)
                if (screenContext == RallyScreenContext.SingleDetails)
                {
                    int attempts = 0;
                    bool keepTrying = true;
                    List<RallyParticipant> participants = new();

                    while (keepTrying && attempts < 2)
                    {
                        context.StartTimer($"Image_{i}_Cycle_{attempts}");

                        var loopGraph = new TopologyGraph(analyzedBlocks, imgW, imgH);

                        // Shifts the boundary slightly upwards to capture top-aligned names
                        double participantSearchY = listStartY - 0.15;
                        var (extractedParticipants, anchors) = _participantNeuron.ExtractParticipants(loopGraph, usedBlocks, participantSearchY);

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
                    if (participants.Any()) await _magnifier.EnrichTroopDetailsAsync(path, participants, analyzedBlocks);

                    foreach (var p in participants)
                    {
                        if (string.IsNullOrWhiteSpace(p.Name) || p.Name == "--") continue;

                        // OCR Noise Correction: Reconciles leader name variations
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
                }

                context.StopTimer($"Image_{i}_Total");
            }

            InferTroopTypes(result, context);

            AuditRally(result, context);

            context.RegisterResult("rally_id", CreateResult(result.RallyId), "Orchestrator_Concat");
            context.RegisterResult("target_name", CreateResult(result.Target.Name), "RallyTargetNeuron");
            context.RegisterResult("leader_name", CreateResult(result.Leader.Name), "RallyHeaderNeuron");
            context.RegisterResult("current_capacity", CreateResult(result.Status.CurrentCapacity), "RallyHeaderNeuron");
        }
        catch (Exception ex)
        {
            context.LogError($"Critical Error in RallyOrchestrator: {ex.Message}\n{ex.StackTrace}");
            _logger.LogError(ex, "Rally Analysis Failed");
        }
        finally
        {
            context.StopTimer("TotalRallyOrchestration");
        }

        return (result, context);
    }

    private void InferTroopTypes(RallyResult result, OcrAnalysisContext context)
    {
        // Identifies the troop types actively participating in this rally
        var globalTotals = new Dictionary<string, long>
        {
            { "Infantry", result.GlobalTroops.Infantry },
            { "Cavalry", result.GlobalTroops.Cavalry },
            { "Archer", result.GlobalTroops.Archer },
            { "Siege", result.GlobalTroops.Siege }
        };

        var activeTroops = globalTotals.Where(t => t.Value > 0).ToList();
        if (!activeTroops.Any()) return; // Aborts inference if no global troops were detected

        foreach (var p in result.Participants)
        {
            foreach (var detail in p.TroopDetails)
            {
                if (detail.Type != "Unknown") continue;

                // INFERENCE PIPELINE 1: Single-Troop Rally Deduction
                // If the global summary contains only one troop type, all participants implicitly sent that type.
                if (activeTroops.Count == 1)
                {
                    detail.Type = activeTroops.First().Key;
                    context.Log($"Logical Inference: Set {p.Name}'s {detail.Count} troops to {detail.Type} (Single-troop rally).");
                    continue;
                }

                // INFERENCE PIPELINE 2: Exact Quantity Matching
                // In mixed rallies, if a participant's sent amount perfectly matches a unique global sum, the type is deduced.
                var exactMatch = activeTroops.Where(t => t.Value == detail.Count).ToList();
                if (exactMatch.Count == 1)
                {
                    detail.Type = exactMatch.First().Key;
                    context.Log($"Logical Inference: Set {p.Name}'s {detail.Count} troops to {detail.Type} (Exact amount match).");
                    continue;
                }

                // INFERENCE PIPELINE 3: Logical Elimination
                // Evaluates mathematical bounds to eliminate impossible troop assignments.
                var mathematicallyPossible = activeTroops.Where(t => t.Value >= detail.Count).ToList();
                if (mathematicallyPossible.Count == 1)
                {
                    detail.Type = mathematicallyPossible.First().Key;
                    context.Log($"Logical Inference: Set {p.Name}'s {detail.Count} troops to {detail.Type} (Logical elimination).");
                }
            }
        }
    }

    private ExtractionResult<T> CreateResult<T>(T val) => new ExtractionResult<T> { Value = val, Confidence = 100, SourceBlock = null };

    private void AuditRally(RallyResult result, OcrAnalysisContext context)
    {
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
                    result.OverallConfidence = 95;
                    context.Log("Audit Passed: Participant sum matches Header capacity.");
                }
                else
                {
                    result.OverallConfidence = Math.Max(50, 90 - (errorRate * 100));
                    string warning = $"Capacity Mismatch: Header says {headerCurrent}, List sums to {sumFromList} (Diff: {diff})";
                    result.Warnings.Add(warning);
                    context.LogWarning("AUDIT_FAIL", warning);
                }
            }
            else
            {
                result.Status.CurrentCapacity = sumFromList;
                result.OverallConfidence = 80;
            }
        }
        else
        {
            // Prevents confidence penalty on MultiList screens where participants are inherently absent.
            result.OverallConfidence = 90;
        }

        if (string.IsNullOrEmpty(result.RallyId) || result.RallyId.Contains("X0Y0")) result.OverallConfidence -= 20;
    }
}
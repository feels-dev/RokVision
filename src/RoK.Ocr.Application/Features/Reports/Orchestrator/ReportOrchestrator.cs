using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Common.Dtos;    
using RoK.Ocr.Application.Common.Models;  
using RoK.Ocr.Application.Features.Reports.Cognitive;
using RoK.Ocr.Application.Features.Reports.Neurons;
using RoK.Ocr.Application.Features.Reports.Services;
using RoK.Ocr.Application.Reports.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;

namespace RoK.Ocr.Application.Features.Reports.Orchestrator;

public class ReportOrchestrator
{
    private readonly IOcrService _ocrService;
    private readonly WarMagnifier _magnifier;
    private readonly IVocabularyLoader _vocabLoader;
    private readonly IImageStorage _storage;
    private readonly ReportScoreCalculator _scoreCalculator;
    private readonly ILogger<ReportOrchestrator> _logger;

    public ReportOrchestrator(
        IOcrService ocrService,
        WarMagnifier magnifier,
        IVocabularyLoader vocabLoader,
        IImageStorage storage,
        ReportScoreCalculator scoreCalculator,
        ILogger<ReportOrchestrator> logger)
    {
        _ocrService = ocrService;
        _magnifier = magnifier;
        _vocabLoader = vocabLoader;
        _storage = storage;
        _scoreCalculator = scoreCalculator;
        _logger = logger;
    }

    public async Task<(ReportResult Data, OcrAnalysisContext Context)> AnalyzeAsync(string imagePath, bool debugMode = false)
    {
        // 1. Initialize Context and Timers
        var context = new OcrAnalysisContext();
        context.StartTimer("TotalOrchestration");
        
        context.Log($"Starting Report Analysis for: {Path.GetFileName(imagePath)}");
        var result = new ReportResult();

        // 2. Initial OCR (Python)
        context.StartTimer("PythonInitialScan");
        var (blocks, width, height, isIsolated, processedImgName) = await _ocrService.AnalyzeReportAsync(imagePath);
        context.StopTimer("PythonInitialScan");
        
        context.Log($"OCR Scan Complete. Found {blocks.Count} blocks. Isolated: {isIsolated}");

        // Populate basic debug info if requested
        if (debugMode)
        {
            context.DebugInfo.Image = new ImageMetaDto 
            { 
                Path = imagePath, 
                // Report returns the processed canvas size, use as reference
                Width = (int)width, 
                Height = (int)height,
                ResizeScale = 1.0 // Python returns normalized coords
            };
            // RawText can be large for reports, construct if needed
            context.DebugInfo.RawText = string.Join("\n", blocks.Select(b => b.Text));
        }

        // 3. Classification and Graph
        context.StartTimer("GraphBuild");
        var nodes = blocks.Select(b => new AnalyzedBlock
        {
            Raw = b,
            CanvasWidth = width,
            CanvasHeight = height
        }).ToList();

        WarBlockClassifier.ClassifyNodes(nodes);
        var graph = new TopologyGraph(nodes, width, height);
        
        // Register anchors for debug
        if (debugMode)
        {
            var anchors = nodes
                .Where(n => n.Type != BlockType.Unknown && n.Type != BlockType.Number)
                .Select(n => n.Type.ToString())
                .Distinct();
            context.RegisterAnchors(anchors);
        }

        result.Type = DetectReportType(nodes);
        context.StopTimer("GraphBuild");
        
        context.Log($"Detected Report Type: {result.Type}");

        // 4. Intelligence and Repair Cycle
        int maxRetries = 2;
        int retryCount = 0;
        bool processingNeeded = true;

        string targetImageForMagnifier = imagePath;
        if (!string.IsNullOrEmpty(processedImgName))
        {
            targetImageForMagnifier = Path.Combine(_storage.GetBasePath(), "uploads", processedImgName);
        }

        while (processingNeeded && retryCount <= maxRetries)
        {
            if (retryCount > 0) context.Log($"--- Repair Cycle {retryCount} ---");
            context.StartTimer($"Cycle_{retryCount}");

            // Execute Neurons and populate result
            ExecuteSpecializedNeurons(graph, nodes, result, context);

            // Consistency Audit (Math)
            AuditConsistency(result, context);

            // Check if repair is needed
            if (!result.IsMathematicallySound() && retryCount < maxRetries)
            {
                context.LogWarning("WARN_MATH_MISMATCH", "Troops calculation mismatch. Initiating Batch Repair.");
                
                // Attempt repair via Magnifier
                await AttemptRepairAsync(targetImageForMagnifier, nodes, context);

                // Reconstruct graph with updated numbers
                graph = new TopologyGraph(nodes, width, height);
                retryCount++;

                // Clear old warnings to re-validate
                result.Warnings.Clear();
            }
            else
            {
                processingNeeded = false;
            }
            
            context.StopTimer($"Cycle_{retryCount}");
        }

        // 5. Metadata and Sanity Check
        ExtractContextMetadata(nodes, result, context);
        RunSanityCheck(result, isIsolated, context);

        // 6. Global Confidence
        result.OverallConfidence = _scoreCalculator.Calculate(result, nodes, isIsolated);

        // Capture final numerical evidence
        CaptureMetricsEvidence(graph, result.Attacker, "atk", context);
        if (result.Type != ReportType.Barbarian)
        {
            CaptureMetricsEvidence(graph, result.Defender, "def", context);
        }

        context.StopTimer("TotalOrchestration");
        context.Log($"Analysis Finished. Overall Confidence: {result.OverallConfidence:F2}%");

        // Cleanup temporary processed image
        if (targetImageForMagnifier != imagePath && File.Exists(targetImageForMagnifier))
        {
            try { File.Delete(targetImageForMagnifier); } catch { }
        }

        return (result, context);
    }

    // --- HELPER METHODS ---

    private void ExecuteSpecializedNeurons(
        TopologyGraph graph,
        List<AnalyzedBlock> nodes,
        ReportResult result,
        OcrAnalysisContext context)
    {
        var commanders = _vocabLoader.GetCommanders();
        var tagNeuron = new AllianceTagNeuron();
        var nameNeuron = new GovernorNameNeuron(commanders);
        var metricNeuron = new WarMetricNeuron();
        var playerCommNeuron = new CommanderNeuron(commanders);

        // Define cutoff height
        var anchorNode = nodes.FirstOrDefault(n => n.Type == BlockType.StatusResult);
        double battleMetricsStartY = anchorNode != null ? anchorNode.NormalizedCenter.Y : 0.4;

        // === ATTACKER ===
        result.Attacker.IsNpc = false;
        var resAtkTag = tagNeuron.Extract(graph, SideLocation.Attacker);
        result.Attacker.AllianceTag = resAtkTag.Tag;
        context.RegisterResult("atk_tag", CreateResult(resAtkTag.Tag, resAtkTag.OriginalBlock), "AllianceTagNeuron");

        result.Attacker.GovernorName = nameNeuron.Extract(graph, resAtkTag.OriginalBlock, SideLocation.Attacker, nodes, resAtkTag.NameSuffix);
        context.RegisterResult("atk_name", CreateResult(result.Attacker.GovernorName, null), "GovernorNameNeuron");

        metricNeuron.PopulateSide(graph, result.Attacker, SideLocation.Attacker, nodes, battleMetricsStartY);

        var commsAtk = playerCommNeuron.Extract(graph, SideLocation.Attacker, nodes);
        result.Attacker.PrimaryCommander = commsAtk.ElementAtOrDefault(0);
        result.Attacker.SecondaryCommander = commsAtk.ElementAtOrDefault(1);

        // === DEFENDER ===
        var npcCommanders = TryIdentifyNpcCommaders(graph, nodes);
        bool isNpcBattle = result.Type == ReportType.Barbarian || npcCommanders.Any();

        if (isNpcBattle)
        {
            result.Type = ReportType.Barbarian;
            result.Defender.IsNpc = true;
            context.Log($"Def: Identified as NPC/Barbarian. Forcing PVE type.");

            result.Defender.PrimaryCommander = npcCommanders.ElementAtOrDefault(0);
            result.Defender.SecondaryCommander = npcCommanders.ElementAtOrDefault(1);
            result.Defender.GovernorName = result.Defender.PrimaryCommander?.CanonicalName ?? "NPC_Entity";

            metricNeuron.PopulateSide(graph, result.Defender, SideLocation.Defender, nodes, battleMetricsStartY);

            var pveNeuron = new PveMetricNeuron();
            result.Defender.PveStats = pveNeuron.Extract(nodes, SideLocation.Defender);
            if (result.Defender.PveStats != null)
            {
                result.Defender.PveStats.EntityLevel = int.TryParse(Regex.Match(result.Defender.GovernorName, @"\d+").Value, out int lvl) ? lvl : 0;
                result.Defender.PveStats.EntityType = result.Type.ToString();
            }
        }
        else
        {
            result.Defender.IsNpc = false;
            var resDefTag = tagNeuron.Extract(graph, SideLocation.Defender);
            result.Defender.AllianceTag = resDefTag.Tag;
            context.RegisterResult("def_tag", CreateResult(resDefTag.Tag, resDefTag.OriginalBlock), "AllianceTagNeuron");

            result.Defender.GovernorName = nameNeuron.Extract(graph, resDefTag.OriginalBlock, SideLocation.Defender, nodes, resDefTag.NameSuffix);
            context.RegisterResult("def_name", CreateResult(result.Defender.GovernorName, null), "GovernorNameNeuron");

            metricNeuron.PopulateSide(graph, result.Defender, SideLocation.Defender, nodes, battleMetricsStartY);

            var commsDef = playerCommNeuron.Extract(graph, SideLocation.Defender, nodes);
            result.Defender.PrimaryCommander = commsDef.ElementAtOrDefault(0);
            result.Defender.SecondaryCommander = commsDef.ElementAtOrDefault(1);
        }

        // Cleanup
        if (string.IsNullOrWhiteSpace(result.Attacker.GovernorName) || result.Attacker.GovernorName.Length < 1) result.Attacker.GovernorName = "--";
        if (string.IsNullOrWhiteSpace(result.Defender.GovernorName) || result.Defender.GovernorName.Length < 1) result.Defender.GovernorName = "--";
    }

    private void CaptureMetricsEvidence(TopologyGraph graph, BattleSide side, string prefix, OcrAnalysisContext context)
    {
        var map = new Dictionary<string, long>
        {
            { "total_units", side.TotalUnits }, { "dead", side.Dead }, { "sev_wounded", side.SeverelyWounded },
            { "sli_wounded", side.SlightlyWounded }, { "remaining", side.Remaining }, { "healed", side.Healed },
            { "kp", side.KillPointsGained }
        };

        foreach (var kvp in map)
        {
            if (kvp.Value <= 0) continue;
            // Register simple evidence
            context.RegisterResult($"{prefix}_{kvp.Key}", new ExtractionResult<long> { Value = kvp.Value, Confidence = 90 }, "WarMetricNeuron");
        }
    }

    private void AuditConsistency(ReportResult report, OcrAnalysisContext context)
    {
        void Check(BattleSide side, string name)
        {
            if (side.TotalUnits <= 0) return;
            long expected = side.TotalUnits + side.Healed;
            long actual = side.Dead + side.SeverelyWounded + side.SlightlyWounded + side.Remaining + side.WatchtowerDamage;
            if (Math.Abs(expected - actual) > 5)
                context.LogWarning("WARN_MATH_MISMATCH", $"[{name}] Math mismatch: Expected {expected} vs Actual {actual} (Diff: {expected - actual})");
        }
        Check(report.Attacker, "Attacker");
        if (report.Type != ReportType.Barbarian) Check(report.Defender, "Defender");
    }

    private async Task AttemptRepairAsync(string imagePath, List<AnalyzedBlock> nodes, OcrAnalysisContext context)
    {
        context.StartTimer("MagnifierBatchRepair");
        
        var lowConfNodes = nodes.Where(n => n.Type == BlockType.Number && n.Raw.Confidence < 0.85).ToList();
        if (!lowConfNodes.Any()) 
        {
            context.StopTimer("MagnifierBatchRepair");
            return;
        }

        context.Log($"Batch Repair: Sending {lowConfNodes.Count} nodes to Python Magnifier.");
        
        // Call Magnifier (passing context for detailed logs)
        var results = await _magnifier.RescanBatchAsync(imagePath, lowConfNodes, context);

        int recovered = 0;
        foreach (var node in lowConfNodes)
        {
            var bestFit = results
                .Where(r => r.Text.All(char.IsDigit) && r.Confidence > node.Raw.Confidence)
                .OrderByDescending(r => r.Confidence)
                .FirstOrDefault();

            if (bestFit != null)
            {
                context.Log($"Repair Success: '{node.Raw.Text}' -> '{bestFit.Text}' ({bestFit.Confidence:P})");
                node.Raw.Text = bestFit.Text;
                node.Raw.Confidence = bestFit.Confidence;
                results.Remove(bestFit);
                recovered++;
            }
        }
        
        // Register Magnifier stats in DebugInfo
        context.RegisterMagnifierAttempt("BatchMathRepair", lowConfNodes.Count, $"Recovered: {recovered}", recovered > 0);
        
        context.StopTimer("MagnifierBatchRepair");
    }

    private void RunSanityCheck(ReportResult result, bool isIsolated, OcrAnalysisContext context)
    {
        if (!isIsolated) context.LogWarning("WARN_NO_ISOLATION", "Report paper was not isolated.");
        if (result.Attacker.AllianceTag != "--" && result.Attacker.GovernorName == "--")
            context.LogWarning("WARN_TAG_BUT_NO_NAME", "Attacker tag found but name is missing.");
    }

    private void ExtractContextMetadata(List<AnalyzedBlock> nodes, ReportResult result, OcrAnalysisContext context)
    {
        var dateMatch = nodes
            .Select(n => Regex.Match(n.Raw.Text, @"(\d{2,4}[/\-]\d{2}([/\-]\d{2,4})?)"))
            .FirstOrDefault(m => m.Success);

        if (dateMatch != null)
        {
            string datePart = dateMatch.Groups[1].Value;
            if (datePart.Length <= 5) datePart = $"{DateTime.Now.Year}/{datePart}";
            if (DateTime.TryParse(datePart.Replace("-", "/"), out DateTime dt))
            {
                result.Timestamp = dt;
                context.RegisterResult("timestamp", new ExtractionResult<string> { Value = dt.ToString("s"), Confidence = 100 }, "MetadataRegex");
            }
        }
    }

    private ReportType DetectReportType(List<AnalyzedBlock> nodes)
    {
        bool isBarbarian = nodes.Any(n => WarVocabulary.BarbarianKeywords.Any(k => n.Raw.Text.Contains(k, StringComparison.OrdinalIgnoreCase)));
        return isBarbarian ? ReportType.Barbarian : ReportType.SingleBattle_PVP;
    }

    private ExtractionResult<T> CreateResult<T>(T val, AnalyzedBlock? block) =>
        new ExtractionResult<T> { Value = val, Confidence = block?.Raw.Confidence ?? 80, SourceBlock = block };

    private List<CommanderEntry> TryIdentifyNpcCommaders(TopologyGraph graph, List<AnalyzedBlock> nodes)
    {
        var npcsVocab = _vocabLoader.GetNpcs();
        var npcCommNeuron = new CommanderNeuron(npcsVocab);
        return npcCommNeuron.Extract(graph, SideLocation.Defender, nodes);
    }
}
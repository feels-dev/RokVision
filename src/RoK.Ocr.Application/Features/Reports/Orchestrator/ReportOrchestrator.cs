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
        var context = new OcrAnalysisContext();
        context.StartTimer("TotalOrchestration");

        context.Log("ReportOrchestrator", $"Starting Report Analysis for: {Path.GetFileName(imagePath)}");
        var result = new ReportResult();

        // 1. Initial OCR Scan via Python Engine
        context.StartTimer("PythonInitialScan");
        var (blocks, width, height, isIsolated, processedImgName) = await _ocrService.AnalyzeReportAsync(imagePath);
        context.StopTimer("PythonInitialScan");

        // Set Image Context and Scale
        context.ImageWidth = (int)width;
        context.ImageHeight = (int)height;
        context.DebugInfo.ImagePath = imagePath;

        context.Log("ReportOrchestrator", $"OCR Scan Complete. Found {blocks.Count} blocks. Isolated: {isIsolated}");

        if (debugMode)
        {
            context.DebugInfo.RawText = string.Join("\n", blocks.Select(b => b.Text));
        }

        // 2. Classification and Graph Building
        context.StartTimer("GraphBuild");
        var nodes = blocks.Select(b => new AnalyzedBlock
        {
            Raw = b,
            CanvasWidth = width,
            CanvasHeight = height
        }).ToList();

        WarBlockClassifier.ClassifyNodes(nodes);
        var graph = new TopologyGraph(nodes, width, height);

        if (debugMode)
        {
            var anchors = nodes.Where(n => n.Type != BlockType.Unknown && n.Type != BlockType.Number).Select(n => n.Type.ToString()).Distinct();
            context.RegisterAnchors(anchors);
        }

        result.Type = DetectReportType(nodes);
        context.StopTimer("GraphBuild");

        context.Log("ReportOrchestrator", $"Detected Report Type: {result.Type}");

        // 3. Intelligence and Repair Cycle
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
            if (retryCount > 0) context.Log("ReportOrchestrator", $"--- Repair Cycle {retryCount} ---");
            context.StartTimer($"Cycle_{retryCount}");

            ExecuteSpecializedNeurons(graph, nodes, result, context);

            // Calls the global consistency auditor
            ConsistencyAuditor.Audit(result, context);

            if (!result.IsMathematicallySound() && retryCount < maxRetries)
            {
                context.LogWarning("ReportOrchestrator", "WARN_MATH_MISMATCH", "Troops calculation mismatch. Initiating Batch Repair.", "HIGH");

                await AttemptRepairAsync(targetImageForMagnifier, nodes, context);

                // Rebuild graph with repaired nodes
                graph = new TopologyGraph(nodes, width, height);
                retryCount++;
                result.Warnings.Clear();
            }
            else
            {
                processingNeeded = false;
            }

            context.StopTimer($"Cycle_{retryCount}");
        }

        // 4. Metadata and Sanity Check
        ExtractContextMetadata(nodes, result, context);
        RunSanityCheck(result, isIsolated, context);

        // 5. Global Confidence Calculation
        result.OverallConfidence = _scoreCalculator.Calculate(result, nodes, isIsolated);

        context.StopTimer("TotalOrchestration");
        context.Log("ReportOrchestrator", $"Analysis Finished. Overall Confidence: {result.OverallConfidence:F2}%");

        // Cleanup temporary magnifier target image if applicable
        if (targetImageForMagnifier != imagePath && File.Exists(targetImageForMagnifier))
        {
            try { File.Delete(targetImageForMagnifier); } catch { }
        }

        return (result, context);
    }

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

        var anchorNode = nodes.FirstOrDefault(n => n.Type == BlockType.StatusResult);
        double battleMetricsStartY = anchorNode != null ? anchorNode.NormalizedCenter.Y : 0.4;

        // ====================================================================
        // 1. ATTACKER ANALYSIS
        // ====================================================================
        result.Attacker.IsNpc = false;

        // Extract Tag
        var resAtkTag = tagNeuron.Extract(graph, SideLocation.Attacker);
        result.Attacker.AllianceTag = resAtkTag.Tag;
        context.RegisterResult("atk_tag", CreateResult(resAtkTag.Tag, resAtkTag.OriginalBlock, resAtkTag.Strategy), "AllianceTagNeuron");

        // Extract Name
        var atkNameResult = nameNeuron.Extract(graph, resAtkTag.OriginalBlock, SideLocation.Attacker, nodes, resAtkTag.NameSuffix);
        result.Attacker.GovernorName = atkNameResult.Value;
        context.RegisterResult("atk_name", atkNameResult, "GovernorNameNeuron");

        // Extract Metrics
        metricNeuron.PopulateSide(graph, result.Attacker, SideLocation.Attacker, nodes, battleMetricsStartY, out var atkMetricBlocks);
        CaptureMetricsEvidence(result.Attacker, "atk", context, atkMetricBlocks, nodes);

        // Extract Commanders
        var commsAtk = playerCommNeuron.Extract(graph, SideLocation.Attacker, nodes);
        if (commsAtk.Count > 0)
        {
            result.Attacker.PrimaryCommander = commsAtk[0].Value;
            context.RegisterResult("atk_primary_commander", commsAtk[0], "CommanderNeuron");
        }
        if (commsAtk.Count > 1)
        {
            result.Attacker.SecondaryCommander = commsAtk[1].Value;
            context.RegisterResult("atk_secondary_commander", commsAtk[1], "CommanderNeuron");
        }

        // ====================================================================
        // 2. DEFENDER ANALYSIS
        // ====================================================================
        var npcCommanders = TryIdentifyNpcCommaders(graph, nodes);
        bool isNpcBattle = result.Type == ReportType.Barbarian || npcCommanders.Any();

        if (isNpcBattle)
        {
            result.Type = ReportType.Barbarian;
            result.Defender.IsNpc = true;
            context.Log("ReportOrchestrator", $"Def: Identified as NPC/Barbarian. Forcing PVE type.");

            if (npcCommanders.Count > 0)
            {
                result.Defender.PrimaryCommander = npcCommanders[0].Value;
                context.RegisterResult("def_primary_commander", npcCommanders[0], "CommanderNeuron");
            }
            if (npcCommanders.Count > 1)
            {
                result.Defender.SecondaryCommander = npcCommanders[1].Value;
                context.RegisterResult("def_secondary_commander", npcCommanders[1], "CommanderNeuron");
            }

            result.Defender.GovernorName = result.Defender.PrimaryCommander?.CanonicalName ?? "NPC_Entity";

            metricNeuron.PopulateSide(graph, result.Defender, SideLocation.Defender, nodes, battleMetricsStartY, out var defNpcMetricBlocks);
            CaptureMetricsEvidence(result.Defender, "def", context, defNpcMetricBlocks, nodes);

            var pveNeuron = new PveMetricNeuron();
            result.Defender.PveStats = pveNeuron.Extract(nodes, SideLocation.Defender);

            // Attempt to extract level from name (e.g., "Level 25 Barbarian")
            if (result.Defender.PveStats != null)
            {
                result.Defender.PveStats.EntityLevel = int.TryParse(Regex.Match(result.Defender.GovernorName, @"\d+").Value, out int lvl) ? lvl : 0;
                result.Defender.PveStats.EntityType = result.Type.ToString();
            }
        }
        else
        {
            result.Defender.IsNpc = false;

            // Extract Tag
            var resDefTag = tagNeuron.Extract(graph, SideLocation.Defender);
            result.Defender.AllianceTag = resDefTag.Tag;
            context.RegisterResult("def_tag", CreateResult(resDefTag.Tag, resDefTag.OriginalBlock, resDefTag.Strategy), "AllianceTagNeuron");

            // Extract Name
            var defNameResult = nameNeuron.Extract(graph, resDefTag.OriginalBlock, SideLocation.Defender, nodes, resDefTag.NameSuffix);
            result.Defender.GovernorName = defNameResult.Value;
            context.RegisterResult("def_name", defNameResult, "GovernorNameNeuron");

            // Extract Metrics
            metricNeuron.PopulateSide(graph, result.Defender, SideLocation.Defender, nodes, battleMetricsStartY, out var defPvpMetricBlocks);
            CaptureMetricsEvidence(result.Defender, "def", context, defPvpMetricBlocks, nodes);

            // Extract Commanders
            var commsDef = playerCommNeuron.Extract(graph, SideLocation.Defender, nodes);
            if (commsDef.Count > 0)
            {
                result.Defender.PrimaryCommander = commsDef[0].Value;
                context.RegisterResult("def_primary_commander", commsDef[0], "CommanderNeuron");
            }
            if (commsDef.Count > 1)
            {
                result.Defender.SecondaryCommander = commsDef[1].Value;
                context.RegisterResult("def_secondary_commander", commsDef[1], "CommanderNeuron");
            }
        }

        // ====================================================================
        // 3. LOGIC FIX: SELF-FIGHT DETECTION (HEADER MISREAD)
        // ====================================================================
        if (!isNpcBattle && IsNameDuplicate(result.Attacker.GovernorName, result.Defender.GovernorName))
        {
            context.LogWarning("Orchestrator", "WARN_SELF_FIGHT", "Attacker and Defender names are identical. Header likely read as Attacker.", "HIGH");

            // Retry Attacker Name Extraction, specifically blacklisting the block we just found
            var blacklist = atkNameResult.SourceBlock;

            // Re-run the neuron with the blacklist constraint
            var retryResult = nameNeuron.Extract(graph, null, SideLocation.Attacker, nodes, "", blacklist);

            if (retryResult.Confidence > 0 && retryResult.Value != "--")
            {
                context.Log("Orchestrator", $"Correction Success: '{result.Attacker.GovernorName}' -> '{retryResult.Value}'");

                result.Attacker.GovernorName = retryResult.Value;

                // Update Context with Correction Flag = true
                context.RegisterResult("atk_name", retryResult, "GovernorNameNeuron", "PaddleOCR_v4", true);

                // If the tag was also duplicated from the header, clear it
                if (result.Attacker.AllianceTag == result.Defender.AllianceTag)
                {
                    result.Attacker.AllianceTag = "--";
                    
                    // CALL FIX: Now using the overloaded method that accepts explicit confidence
                    context.RegisterResult("atk_tag", CreateResult("--", 0, null, "Correction_Cleared"), "LogicCorrection", "None", true);
                }
            }
        }

        // Final safe defaults
        if (string.IsNullOrWhiteSpace(result.Attacker.GovernorName)) result.Attacker.GovernorName = "--";
        if (string.IsNullOrWhiteSpace(result.Defender.GovernorName)) result.Defender.GovernorName = "--";
    }

    private bool IsNameDuplicate(string name1, string name2)
    {
        if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2)) return false;
        if (name1 == "--" || name2 == "--") return false;
        if (name1.Length < 3 || name2.Length < 3) return false;

        return Fuzz.Ratio(name1, name2) > 90;
    }

    private void CaptureMetricsEvidence(BattleSide side, string prefix, OcrAnalysisContext context, List<AnalyzedBlock> metricBlocks, List<AnalyzedBlock> allNodes)
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

            var sourceBlock = metricBlocks.FirstOrDefault(b => b.Raw.Text.Contains(kvp.Value.ToString()));

            if (sourceBlock == null)
                sourceBlock = allNodes.FirstOrDefault(b => b.Raw.Text.Contains(kvp.Value.ToString()));

            context.RegisterResult($"{prefix}_{kvp.Key}",
                new ExtractionResult<long> { Value = kvp.Value, Confidence = sourceBlock?.Raw.Confidence * 100 ?? 85, SourceBlock = sourceBlock },
                "WarMetricNeuron");
        }
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

        context.Log("ReportOrchestrator", $"Batch Repair: Sending {lowConfNodes.Count} nodes to Python Magnifier.");

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
                context.Log("ReportOrchestrator", $"Repair Success: '{node.Raw.Text}' -> '{bestFit.Text}' ({bestFit.Confidence:P})");
                node.Raw.Text = bestFit.Text;
                node.Raw.Confidence = bestFit.Confidence;
                results.Remove(bestFit);
                recovered++;
            }
        }

        context.RegisterMagnifierAttempt("BatchMathRepair", lowConfNodes.Count, $"Recovered: {recovered}", recovered > 0);
        context.StopTimer("MagnifierBatchRepair");
    }

    private void RunSanityCheck(ReportResult result, bool isIsolated, OcrAnalysisContext context)
    {
        if (!isIsolated) context.LogWarning("SanityCheck", "WARN_NO_ISOLATION", "Report paper was not isolated.", "LOW");
        if (result.Attacker.AllianceTag != "--" && result.Attacker.GovernorName == "--")
            context.LogWarning("SanityCheck", "WARN_TAG_BUT_NO_NAME", "Attacker tag found but name is missing.", "MEDIUM");
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
                context.RegisterResult("timestamp", new ExtractionResult<string> { Value = dt.ToString("s"), Confidence = 100, Strategy = "Regex_DateMatch" }, "MetadataRegex");
            }
        }
    }

    private ReportType DetectReportType(List<AnalyzedBlock> nodes)
    {
        bool isBarbarian = nodes.Any(n => WarVocabulary.BarbarianKeywords.Any(k => n.Raw.Text.Contains(k, StringComparison.OrdinalIgnoreCase)));
        return isBarbarian ? ReportType.Barbarian : ReportType.SingleBattle_PVP;
    }

    private List<ExtractionResult<CommanderEntry>> TryIdentifyNpcCommaders(TopologyGraph graph, List<AnalyzedBlock> nodes)
    {
        var npcsVocab = _vocabLoader.GetNpcs();
        var convertedVocab = npcsVocab.Select(npc => new CommanderEntry
        {
            Id = npc.Id,
            CanonicalName = npc.CanonicalName,
            Rarity = npc.Rarity,
            Expertise = npc.Expertise,
            Labels = npc.Labels
        }).ToList();

        var npcCommNeuron = new CommanderNeuron(convertedVocab);
        return npcCommNeuron.Extract(graph, SideLocation.Defender, nodes);
    }

    // =================================================================================
    // HELPER METHODS (OVERLOADED)
    // =================================================================================

    // Overload 1: Automatic Confidence from Block (Scales 0.0-1.0 to 0-100)
    private ExtractionResult<T> CreateResult<T>(T val, AnalyzedBlock? block, string strategy = "Default") =>
        new ExtractionResult<T> 
        { 
            Value = val, 
            Confidence = block?.Raw.Confidence * 100 ?? 80, 
            SourceBlock = block, 
            Strategy = strategy 
        };

    // Overload 2: Manual Confidence (For explicit overwrites like 0 or 100)
    private ExtractionResult<T> CreateResult<T>(T val, double manualConfidence, AnalyzedBlock? block, string strategy) =>
        new ExtractionResult<T> 
        { 
            Value = val, 
            Confidence = manualConfidence, 
            SourceBlock = block, 
            Strategy = strategy 
        };
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO; 
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; 
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Application.Features.Reports.Neurons;
using RoK.Ocr.Application.Features.Reports.Cognitive;
using RoK.Ocr.Application.Features.Reports.Services;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Reports.Constants;

namespace RoK.Ocr.Application.Features.Reports.Orchestrator;

public class ReportOrchestrator
{
    private readonly IOcrService _ocrService;
    private readonly WarMagnifier _magnifier;
    private readonly IVocabularyLoader _vocabLoader;
    private readonly IImageStorage _storage;
    private readonly ReportScoreCalculator _scoreCalculator; // Logic extracted
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

    public async Task<(ReportResult Data, string RawText)> AnalyzeAsync(string imagePath)
    {
        var result = new ReportResult();

        // 1. Initial OCR and paper isolation
        // This calls the optimized Python endpoint with Resize
        var (blocks, width, height, isIsolated, processedImgName) = await _ocrService.AnalyzeReportAsync(imagePath);

        string rawText = string.Join(" | ", blocks.Select(b => b.Text));

        // 2. Mapping to analyzed nodes
        var nodes = blocks.Select(b => new AnalyzedBlock
        {
            Raw = b,
            CanvasWidth = width,
            CanvasHeight = height
        }).ToList();

        // 3. Classification and Topology
        WarBlockClassifier.ClassifyNodes(nodes);
        var graph = new TopologyGraph(nodes, width, height);
        result.Type = DetectReportType(nodes);

        // 4. Intelligence Cycle & Auto-Healing
        int maxRetries = 2;
        int retryCount = 0;
        bool processingNeeded = true;

        string targetImageForMagnifier = imagePath;
        if (!string.IsNullOrEmpty(processedImgName))
        {
            // We use the processed image (sharpened/isolated) for repairs
            targetImageForMagnifier = Path.Combine(_storage.GetBasePath(), "uploads", processedImgName);
        }

        while (processingNeeded && retryCount <= maxRetries)
        {
            ExecuteSpecializedNeurons(graph, nodes, result);

            result.Warnings.RemoveAll(w => w.Contains("Mismatch"));
            ConsistencyAuditor.Audit(result);

            // If math is wrong, trigger the Repair Cycle
            if (!result.IsMathematicallySound() && retryCount < maxRetries)
            {
                await AttemptRepairAsync(targetImageForMagnifier, nodes, retryCount);
                
                // Rebuild graph with potentially updated numbers
                graph = new TopologyGraph(nodes, width, height);
                retryCount++;
            }
            else
            {
                processingNeeded = false;
            }
        }

        // 5. Metadata and Sanity Checks
        ExtractContextMetadata(nodes, result);
        RunSanityCheck(result, isIsolated);

        // 6. Dynamic Confidence Calculation
        result.OverallConfidence = _scoreCalculator.Calculate(result, nodes, isIsolated);

        // 7. Cleanup temporary files
        // (Optional: Keep for debugging if needed, otherwise delete)
        if (targetImageForMagnifier != imagePath && File.Exists(targetImageForMagnifier))
        {
            try { File.Delete(targetImageForMagnifier); } catch { }
        }

        return (result, rawText);
    }

    private ReportType DetectReportType(List<AnalyzedBlock> nodes)
    {
        bool isBarbarian = nodes.Any(n =>
            WarVocabulary.BarbarianKeywords.Any(key =>
                RokCognitiveTools.CalculateSimilarity(n.Raw.Text, key) > 0.85 ||
                n.Raw.Text.Contains(key, StringComparison.OrdinalIgnoreCase)
            )
        );

        return isBarbarian ? ReportType.Barbarian : ReportType.SingleBattle_PVP;
    }

    private void ExecuteSpecializedNeurons(TopologyGraph graph, List<AnalyzedBlock> nodes, ReportResult result)
    {
        var commanders = _vocabLoader.GetCommanders();
        var npcsVocab = _vocabLoader.GetNpcs();

        var tagNeuron = new AllianceTagNeuron();
        var nameNeuron = new GovernorNameNeuron(commanders);
        var metricNeuron = new WarMetricNeuron();

        var playerCommNeuron = new CommanderNeuron(commanders);
        var npcCommNeuron = new CommanderNeuron(npcsVocab);

        var anchorNode = nodes.FirstOrDefault(n =>
            n.Type == BlockType.StatusResult ||
            WarVocabulary.VictoryTerms.Any(v => n.Raw.Text.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
            WarVocabulary.DefeatTerms.Any(d => n.Raw.Text.Contains(d, StringComparison.OrdinalIgnoreCase))
        );

        double battleMetricsStartY = anchorNode != null ? anchorNode.NormalizedCenter.Y : 0.4;

        // PHASE 1: ATTACKER
        result.Attacker.IsNpc = false;

        var resAtk = tagNeuron.Extract(graph, SideLocation.Attacker);
        result.Attacker.AllianceTag = resAtk.Tag;
        result.Attacker.GovernorName = nameNeuron.Extract(
            graph,
            resAtk.OriginalBlock,
            SideLocation.Attacker,
            nodes,
            resAtk.NameSuffix
        );

        metricNeuron.PopulateSide(graph, result.Attacker, SideLocation.Attacker, nodes, battleMetricsStartY);

        var commandersAtk = playerCommNeuron.Extract(graph, SideLocation.Attacker, nodes);
        result.Attacker.PrimaryCommander = commandersAtk.ElementAtOrDefault(0);
        result.Attacker.SecondaryCommander = commandersAtk.ElementAtOrDefault(1);


        // PHASE 2: DEFENDER
        if (result.Type == ReportType.Barbarian)
        {
            result.Defender.IsNpc = true;
            result.Defender.AllianceTag = "--";

            var barbBlock = nodes.FirstOrDefault(n =>
                WarVocabulary.BarbarianKeywords.Any(key => n.Raw.Text.Contains(key, StringComparison.OrdinalIgnoreCase))
            );
            result.Defender.GovernorName = barbBlock != null ? barbBlock.Raw.Text.Trim() : "NPC_Entity";

            var bossesFound = npcCommNeuron.Extract(graph, SideLocation.Defender, nodes);
            result.Defender.PrimaryCommander = bossesFound.ElementAtOrDefault(0);
            result.Defender.SecondaryCommander = bossesFound.ElementAtOrDefault(1);

            metricNeuron.PopulateSide(graph, result.Defender, SideLocation.Defender, nodes, battleMetricsStartY);

            var pveNeuron = new PveMetricNeuron();
            result.Defender.PveStats = pveNeuron.Extract(nodes, SideLocation.Defender);
            // Enrich PveStats with EntityLevel if needed
            if (result.Defender.PveStats != null)
            {
                result.Defender.PveStats.EntityLevel = int.TryParse(Regex.Match(result.Defender.GovernorName, @"\d+").Value, out int lvl) ? lvl : 0;
                result.Defender.PveStats.EntityType = result.Type.ToString();
            }
        }
        else
        {
            result.Defender.IsNpc = false;

            var resDef = tagNeuron.Extract(graph, SideLocation.Defender);
            result.Defender.AllianceTag = resDef.Tag;

            result.Defender.GovernorName = nameNeuron.Extract(
                graph,
                resDef.OriginalBlock,
                SideLocation.Defender,
                nodes,
                resDef.NameSuffix
            );

            metricNeuron.PopulateSide(graph, result.Defender, SideLocation.Defender, nodes, battleMetricsStartY);

            var commandersDef = playerCommNeuron.Extract(graph, SideLocation.Defender, nodes);
            result.Defender.PrimaryCommander = commandersDef.ElementAtOrDefault(0);
            result.Defender.SecondaryCommander = commandersDef.ElementAtOrDefault(1);
        }

        // PHASE 3: FINAL CLEANUP
        if (string.IsNullOrWhiteSpace(result.Attacker.GovernorName) || result.Attacker.GovernorName.Length < 1)
            result.Attacker.GovernorName = "--";

        if (string.IsNullOrWhiteSpace(result.Defender.GovernorName) || result.Defender.GovernorName.Length < 1)
            result.Defender.GovernorName = "--";

        if (result.Attacker.PrimaryCommander != null &&
            result.Attacker.GovernorName.Contains(result.Attacker.PrimaryCommander.CanonicalName))
        {
            result.Attacker.GovernorName = "--";
        }
    }

    private void ExtractContextMetadata(List<AnalyzedBlock> nodes, ReportResult result)
    {
        var dateMatch = nodes
            .Select(n => Regex.Match(n.Raw.Text, @"(\d{2,4}[/\-]\d{2}([/\-]\d{2,4})?)"))
            .FirstOrDefault(m => m.Success);

        if (dateMatch != null && dateMatch.Success)
        {
            string datePart = dateMatch.Groups[1].Value;
            if (datePart.Length <= 5) datePart = $"{DateTime.Now.Year}/{datePart}";
            if (DateTime.TryParse(datePart.Replace("-", "/"), out DateTime dt))
                result.Timestamp = dt;
        }

        var coordMatch = nodes
            .Select(n => Regex.Match(n.Raw.Text, @"X:?\s*(\d+)\D*Y:?\s*(\d+)", RegexOptions.IgnoreCase))
            .FirstOrDefault(m => m.Success);

        if (coordMatch != null && coordMatch.Success)
        {
            result.MapCoordinates = $"X:{coordMatch.Groups[1].Value} Y:{coordMatch.Groups[2].Value}";
        }
    }

    /// <summary>
    /// Executes the Self-Healing process using Batch Processing.
    /// Sends all low-confidence nodes to Python in a single shot.
    /// </summary>
    private async Task AttemptRepairAsync(string imagePath, List<AnalyzedBlock> nodes, int currentRetry)
    {
        // 1. Identify low confidence numeric nodes that likely caused the math mismatch
        var lowConfidenceNodes = nodes
            .Where(n => n.Type == BlockType.Number && n.Raw.Confidence < 0.85) // Heuristic threshold
            .ToList();

        if (!lowConfidenceNodes.Any()) return;

        // 2. Call Batch Magnifier (OPTIMIZATION: 1 HTTP Request instead of N)
        // Python returns a list of "Best Guesses" from the specialized filters
        var repairResults = await _magnifier.RescanBatchAsync(imagePath, lowConfidenceNodes);

        if (!repairResults.Any()) return;

        // 3. Apply Repairs (Smart Matching)
        // Since we lost the direct link (ID) in the simplistic response, we try to match by value proximity
        // or simply assume that if the Magnifier returns a valid number, it replaces the bad one.
        
        // Refined Logic:
        // We iterate over the nodes we sent for repair. 
        // Ideally, we should have the ID mapping, but for now we look for the best match in the pool.
        
        foreach (var node in lowConfidenceNodes)
        {
            // We look for a result in the repair pool that is:
            // A) Strictly numeric
            // B) Has better confidence than what we currently have
            // C) (Optional) Is numerically plausible (e.g. not 10x larger/smaller if it was just a smudge)
            
            var betterCandidate = repairResults
                .Where(r => IsNumeric(r.Text))
                .OrderByDescending(r => r.Confidence)
                .FirstOrDefault();

            if (betterCandidate != null && betterCandidate.Confidence > node.Raw.Confidence)
            {
                // Apply fix
                _logger.LogInformation("Repair Success: {Old} ({ConfOld:P}) -> {New} ({ConfNew:P})", 
                    node.Raw.Text, node.Raw.Confidence, betterCandidate.Text, betterCandidate.Confidence);

                node.Raw.Text = betterCandidate.Text;
                node.Raw.Confidence = betterCandidate.Confidence;
                
                // Remove used candidate to avoid reusing it for another node 
                // (Simple greedy consumption)
                repairResults.Remove(betterCandidate);
            }
        }
    }

    private bool IsNumeric(string t) => Regex.IsMatch(t, @"^\d+$");

    private void RunSanityCheck(ReportResult result, bool isIsolated)
    {
        if (!isIsolated)
        {
            result.Warnings.Add("WARN_IMAGE_NOT_ISOLATED: The report paper was not automatically isolated. Results may be inaccurate.");
        }

        if (result.Attacker.AllianceTag != "--" && result.Attacker.GovernorName == "--")
        {
            result.Warnings.Add("WARN_UNSUPPORTED_CHARACTERS: Alliance tag detected, but governor name is empty.");
        }

        if (!result.IsMathematicallySound())
        {
            if (result.Attacker.TotalUnits == 0)
                result.Warnings.Add("WARN_DATA_MISSING_TOTAL_UNITS: 'Total Units' field missing.");
            else
                result.Warnings.Add("WARN_MATH_MISMATCH: Troop sums do not match totals.");
        }

        if (result.Attacker.GovernorName == "--" && result.Attacker.TotalUnits > 0)
        {
            result.Warnings.Add("WARN_NAME_IDENTIFICATION_FAILED: Metrics read, but name not identified.");
        }
        
        double nameSimilarity = RokCognitiveTools.CalculateSimilarity(result.Attacker.GovernorName, result.Defender.GovernorName);
        if (nameSimilarity > 0.80 && result.Attacker.GovernorName != "--")
        {
            result.Warnings.Add("WARN_DUPLICATE_NAMES: Attacker and Defender names are identical.");
        }

        if (result.Attacker.GovernorName.Contains(result.Attacker.AllianceTag) && result.Attacker.AllianceTag != "--")
        {
            result.Warnings.Add("WARN_HEADER_READ_AS_NAME: Name contains tag residues.");
        }
    }
}
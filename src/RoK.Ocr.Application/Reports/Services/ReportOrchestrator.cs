using System;
using System.Collections.Generic;
using System.Linq;
using System.IO; // Added explicitly for Path and File usage
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for Serilog/Logging
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Reports;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Application.Reports.Neurons;
using RoK.Ocr.Application.Reports.Cognitive;
using RoK.Ocr.Application.Reports.Magnifier;
using RoK.Ocr.Application.Shared.Cognitive;
using RoK.Ocr.Application.Reports.Constants;
using RoK.Ocr.Application.Cognitive;

namespace RoK.Ocr.Application.Reports.Services;

public class ReportOrchestrator
{
    private readonly IOcrService _ocrService;
    private readonly WarMagnifier _magnifier;
    private readonly IVocabularyLoader _vocabLoader;
    private readonly IImageStorage _storage;
    private readonly ILogger<ReportOrchestrator> _logger; // Logger field added

    public ReportOrchestrator(
        IOcrService ocrService,
        WarMagnifier magnifier,
        IVocabularyLoader vocabLoader,
        IImageStorage storage,
        ILogger<ReportOrchestrator> logger) // Logger injected
    {
        _ocrService = ocrService;
        _magnifier = magnifier;
        _vocabLoader = vocabLoader;
        _storage = storage;
        _logger = logger;
    }

    public async Task<(ReportResult Data, string RawText)> AnalyzeAsync(string imagePath)
    {
        var result = new ReportResult();

        // 1. Initial OCR and paper isolation
        var (blocks, width, height, isIsolated, processedImgName) = await _ocrService.AnalyzeReportAsync(imagePath);

        // Capture the RawText locally since it's no longer in the result class
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
            targetImageForMagnifier = Path.Combine(_storage.GetBasePath(), "uploads", processedImgName);
        }

        while (processingNeeded && retryCount <= maxRetries)
        {
            ExecuteSpecializedNeurons(graph, nodes, result);

            result.Warnings.RemoveAll(w => w.Contains("Mismatch"));
            ConsistencyAuditor.Audit(result);

            if (!result.IsMathematicallySound() && retryCount < maxRetries)
            {
                await AttemptRepairAsync(targetImageForMagnifier, nodes, retryCount);
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
        result.OverallConfidence = CalculateDynamicConfidence(result, nodes, isIsolated);

        // 7. Cleanup temporary files
        if (targetImageForMagnifier != imagePath && File.Exists(targetImageForMagnifier))
        {
            try { File.Delete(targetImageForMagnifier); } catch { }
        }

        // Return both the structured data and the raw text
        return (result, rawText);
    }

    private ReportType DetectReportType(List<AnalyzedBlock> nodes)
    {
        // Checks if any block on screen matches Barbarian vocabulary
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
        // 1. VOCABULARY LOADING
        // Fetch both player commanders and the NPC/Boss list
        var commanders = _vocabLoader.GetCommanders();
        var npcsVocab = _vocabLoader.GetNpcs();

        // 2. SPECIALIZED NEURONS INITIALIZATION
        var tagNeuron = new AllianceTagNeuron();
        var nameNeuron = new GovernorNameNeuron(commanders);
        var metricNeuron = new WarMetricNeuron();

        // Specific Commander Neurons for each context
        var playerCommNeuron = new CommanderNeuron(commanders);
        var npcCommNeuron = new CommanderNeuron(npcsVocab);

        // 3. VISUAL ANCHOR DETECTION (Where the header ends)
        var anchorNode = nodes.FirstOrDefault(n =>
            n.Type == BlockType.StatusResult ||
            WarVocabulary.VictoryTerms.Any(v => n.Raw.Text.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
            WarVocabulary.DefeatTerms.Any(d => n.Raw.Text.Contains(d, StringComparison.OrdinalIgnoreCase))
        );

        // Defines the starting Y point to ignore troop summaries at the top of the screenshot
        double battleMetricsStartY = anchorNode != null ? anchorNode.NormalizedCenter.Y : 0.4;

        // =================================================================
        // PHASE 1: ATTACKER (Always treated as Player/Governor)
        // =================================================================

        result.Attacker.IsNpc = false;

        // A. Alliance and Name Extraction (Sniper Mode)
        var resAtk = tagNeuron.Extract(graph, SideLocation.Attacker);
        result.Attacker.AllianceTag = resAtk.Tag;
        result.Attacker.GovernorName = nameNeuron.Extract(
            graph,
            resAtk.OriginalBlock,
            SideLocation.Attacker,
            nodes,
            resAtk.NameSuffix
        );

        // B. War Metrics (Below the anchor)
        metricNeuron.PopulateSide(graph, result.Attacker, SideLocation.Attacker, nodes, battleMetricsStartY);

        // C. Commanders (Using player vocabulary)
        var commandersAtk = playerCommNeuron.Extract(graph, SideLocation.Attacker, nodes);
        result.Attacker.PrimaryCommander = commandersAtk.ElementAtOrDefault(0);
        result.Attacker.SecondaryCommander = commandersAtk.ElementAtOrDefault(1);


        // =================================================================
        // PHASE 2: DEFENDER (Dynamic Differentiation PvP vs PvE)
        // =================================================================

        if (result.Type == ReportType.Barbarian)
        {
            // -------------------------------------------------
            // SCENARIO PvE (Barbarians, Forts, Marauders)
            // -------------------------------------------------
            result.Defender.IsNpc = true;
            result.Defender.AllianceTag = "--"; // NPCs do not have alliances

            // A. Entity Dynamic Name (e.g., "Lvl 10 Barbarian")
            var barbBlock = nodes.FirstOrDefault(n =>
                WarVocabulary.BarbarianKeywords.Any(key => n.Raw.Text.Contains(key, StringComparison.OrdinalIgnoreCase))
            );
            result.Defender.GovernorName = barbBlock != null ? barbBlock.Raw.Text.Trim() : "NPC_Entity";

            // B. Boss Commanders (Calvin, Kranitos, etc. via NpcsVocabulary.json)
            var bossesFound = npcCommNeuron.Extract(graph, SideLocation.Defender, nodes);
            result.Defender.PrimaryCommander = bossesFound.ElementAtOrDefault(0);
            result.Defender.SecondaryCommander = bossesFound.ElementAtOrDefault(1);

            // C. Combat Metrics (If PvE displays units, like in Forts)
            metricNeuron.PopulateSide(graph, result.Defender, SideLocation.Defender, nodes, battleMetricsStartY);

            // D. EXCLUSIVE PvE METRIC: Damage Received Percentage (-43.2%)
            var damagePercentBlock = nodes
                .Where(n => n.Raw.Text.Contains("%"))
                .OrderByDescending(n => n.Raw.Confidence)
                .FirstOrDefault(n => n.NormalizedCenter.X > 0.5); // Defender Side

            if (damagePercentBlock != null)
            {
                var match = Regex.Match(damagePercentBlock.Raw.Text, @"(\d+[\.,]\d+)");
                if (match.Success)
                {
                    // Initializes the PvE details object in BattleSide
                    result.Defender.PveStats = new PveDetails
                    {
                        DamageReceivedPercentage = double.Parse(
                            match.Value.Replace(",", "."),
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                        // Tries to extract the level (e.g., "Lvl 10" -> 10)
                        EntityLevel = int.TryParse(Regex.Match(result.Defender.GovernorName, @"\d+").Value, out int lvl) ? lvl : 0,
                        EntityType = result.Type.ToString()
                    };
                }
            }
        }
        else
        {
            // -------------------------------------------------
            // SCENARIO PvP (Battle between Players)
            // -------------------------------------------------
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

        // =================================================================
        // PHASE 3: FINAL NORMALIZATION AND CLEANUP
        // =================================================================

        // Remove residues of misinterpreted names
        if (string.IsNullOrWhiteSpace(result.Attacker.GovernorName) || result.Attacker.GovernorName.Length < 1)
            result.Attacker.GovernorName = "--";

        if (string.IsNullOrWhiteSpace(result.Defender.GovernorName) || result.Defender.GovernorName.Length < 1)
            result.Defender.GovernorName = "--";

        // ANTI-DUPLICITY: If player name equals commander name due to OCR error, clear it.
        if (result.Attacker.PrimaryCommander != null &&
            result.Attacker.GovernorName.Contains(result.Attacker.PrimaryCommander.CanonicalName))
        {
            result.Attacker.GovernorName = "--";
        }
    }

    private void ExtractContextMetadata(List<AnalyzedBlock> nodes, ReportResult result)
    {
        // Date Regex MM/DD with year injection
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

        // Coordinate Precision Regex (X:000 Y:000)
        var coordMatch = nodes
            .Select(n => Regex.Match(n.Raw.Text, @"X:?\s*(\d+)\D*Y:?\s*(\d+)", RegexOptions.IgnoreCase))
            .FirstOrDefault(m => m.Success);

        if (coordMatch != null && coordMatch.Success)
        {
            result.MapCoordinates = $"X:{coordMatch.Groups[1].Value} Y:{coordMatch.Groups[2].Value}";
        }
    }

    private async Task AttemptRepairAsync(string imagePath, List<AnalyzedBlock> nodes, int currentRetry)
    {
        var lowConfidenceNodes = nodes
            .Where(n => n.Type == BlockType.Number && n.Raw.Confidence < 0.80)
            .OrderBy(n => n.Raw.Confidence)
            .ToList();

        foreach (var node in lowConfidenceNodes)
        {
            var improvedBlocks = await _magnifier.RescanZoneAsync(imagePath, node.Raw.Box, $"Repair_R{currentRetry}");
            if (improvedBlocks.Any())
            {
                var best = improvedBlocks.OrderByDescending(b => b.Confidence).First();
                if (best.Confidence > node.Raw.Confidence || currentRetry == 1)
                {
                    node.Raw.Text = best.Text;
                    node.Raw.Confidence = best.Confidence;
                }
            }
        }
    }

    private double CalculateDynamicConfidence(ReportResult result, List<AnalyzedBlock> usedNodes, bool isIsolated)
    {
        double score = 0;

        // 1. MATHEMATICAL PILLAR (Max: 50 points)
        // This is the strongest validator in RoK.
        if (result.IsMathematicallySound())
        {
            score += 50;
        }
        else
        {
            // If math doesn't add up, we calculate how far off it is.
            // Small difference (1 digit reading error) loses little.
            // Large difference loses a lot.
            long atkDiff = Math.Abs((result.Attacker.TotalUnits + result.Attacker.Healed) -
                           (result.Attacker.Dead + result.Attacker.SeverelyWounded +
                            result.Attacker.SlightlyWounded + result.Attacker.Remaining + result.Attacker.WatchtowerDamage));

            if (atkDiff > 0 && result.Attacker.TotalUnits > 0)
            {
                double errorRatio = (double)atkDiff / result.Attacker.TotalUnits;
                score += Math.Max(0, 25 * (1 - errorRatio)); // Gives up to 25 points if the error is tiny
            }
        }

        // 2. OCR PILLAR (Max: 20 points)
        // Average confidence of the blocks we actually used to fill the object
        if (usedNodes.Any())
        {
            double avgOcrConf = usedNodes.Average(n => n.Raw.Confidence) * 100;
            score += (avgOcrConf * 0.20);
        }

        // 3. SEMANTIC PILLAR (Max: 20 points)
        // We reward filling in essential fields
        int fieldsFound = 0;
        if (result.Attacker.GovernorName != "--") fieldsFound++;
        if (result.Attacker.AllianceTag != "--") fieldsFound++;
        if (result.Attacker.PrimaryCommander != null) fieldsFound++;
        if (result.Defender.GovernorName != "--") fieldsFound++;

        // Each essential field is worth 5 points
        score += (fieldsFound * 5);

        // 4. INFRASTRUCTURE PILLAR (Max: 10 points)
        if (isIsolated) score += 10;

        // --- PENALTIES ---
        // If attacker name wasn't found, it's a "blind" report
        if (result.Attacker.GovernorName == "--") score -= 20;

        if (result.Warnings.Any(w => w.Contains("DUPLICATE_NAMES") || w.Contains("HEADER_READ")))
        {
            score -= 40; // Heavy penalty for identity error
        }

        // Ensures score stays between 0 and 100
        return Math.Clamp(score, 0, 100);
    }

    private void RunSanityCheck(ReportResult result, bool isIsolated)
    {
        // 1. Isolation Check (Cropped or Bad Screenshot)
        if (!isIsolated)
        {
            result.Warnings.Add("WARN_IMAGE_NOT_ISOLATED: The report paper was not automatically isolated. This usually happens with cropped screenshots or high visual interference. Results may be inaccurate.");
        }

        // 2. Unsupported Characters Check (CJK)
        // If we have the alliance tag but the name is empty, it's 90% chance of being Japanese/Chinese/Korean
        if (result.Attacker.AllianceTag != "--" && result.Attacker.GovernorName == "--")
        {
            result.Warnings.Add("WARN_UNSUPPORTED_CHARACTERS: Alliance tag detected, but governor name is empty. Possible use of Asian characters (CJK) not supported in this version.");
        }

        // 3. Mathematical Integrity Check
        if (!result.IsMathematicallySound())
        {
            if (result.Attacker.TotalUnits == 0)
            {
                result.Warnings.Add("WARN_DATA_MISSING_TOTAL_UNITS: The 'Total Units' field was not found. Mathematical audit could not be performed.");
            }
            else
            {
                result.Warnings.Add("WARN_MATH_MISMATCH: The sum of troops (dead, wounded, etc.) does not match the total. Check if the screenshot has windows (chat/menus) covering the numbers.");
            }
        }

        // 4. Governor Name Check (Most important data)
        if (result.Attacker.GovernorName == "--" && result.Attacker.TotalUnits > 0)
        {
            result.Warnings.Add("WARN_NAME_IDENTIFICATION_FAILED: Metrics were read, but the player name was not identified. Check if the name is hidden or in an unconventional format.");
        }
        // 5. Duplicate Names Check (Fuzzy Match)
        // If attacker name is too similar to defender name, something went wrong.
        double nameSimilarity = RokCognitiveTools.CalculateSimilarity(result.Attacker.GovernorName, result.Defender.GovernorName);
        if (nameSimilarity > 0.80 && result.Attacker.GovernorName != "--")
        {
            result.Warnings.Add("WARN_DUPLICATE_NAMES: Attacker and Defender have nearly identical names. The OCR may have read the report header by mistake.");
        }

        // 6. Tag in Name Check
        // If the name contains the Tag (e.g., JM74JITRIOS...), it picked up the list header
        if (result.Attacker.GovernorName.Contains(result.Attacker.AllianceTag) && result.Attacker.AllianceTag != "--")
        {
            result.Warnings.Add("WARN_HEADER_READ_AS_NAME: The attacker's name seems to have been extracted from the list header, not the main report.");
        }
    }
}
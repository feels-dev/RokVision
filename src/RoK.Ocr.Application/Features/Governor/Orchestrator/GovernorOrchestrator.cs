using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Application.Common.Interfaces;
using RoK.Ocr.Application.Common.Models; 
using RoK.Ocr.Application.Common.Dtos;   
using RoK.Ocr.Application.Features.Governor.Neurons;
using RoK.Ocr.Application.Features.Governor.Services;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.Governor.Orchestrator;

public class GovernorOrchestrator
{
    private readonly GovernorMagnifier _magnifier;

    // Specialist Neurons
    private readonly IdNeuron _idNeuron = new();
    private readonly NameNeuron _nameNeuron = new();
    private readonly AllianceNeuron _allianceNeuron = new();
    private readonly CivNeuron _civNeuron = new();
    private readonly StatsNeuron _statsNeuron = new(requireBigNumber: true);

    public GovernorOrchestrator(GovernorMagnifier magnifier)
    {
        _magnifier = magnifier;
    }

    public async Task<(GovernorProfile Profile, OcrAnalysisContext Context)> AnalyzeAsync(
        string imagePath, 
        List<OcrBlock> rawBlocks, 
        int draftId = 0)
    {
        // 1. Initialize Audit Context and Total Timer
        var context = new OcrAnalysisContext();
        context.StartTimer("TotalOrchestration");
        
        context.Log($"Starting orchestration for image: {System.IO.Path.GetFileName(imagePath)}");

        var finalData = new GovernorProfile();

        // 2. Initial Classification (with timing)
        context.StartTimer("Classification");
        context.Log($"Classifying {rawBlocks.Count} raw blocks...");
        var analyzedBlocks = BlockClassifier.Classify(rawBlocks);
        context.StopTimer("Classification");
        
        int attempts = 0;
        bool keepTrying = true;

        // Self-Correction Loop
        while (keepTrying && attempts < 3)
        {
            context.Log($"--- Cycle {attempts + 1} Start ---");
            context.StartTimer($"Cycle_{attempts + 1}");

            var usedBlocks = new HashSet<AnalyzedBlock>();
            var anchors = MapAnchors(analyzedBlocks);

            // Register Anchors in Debug (first pass only)
            if (attempts == 0)
            {
                context.RegisterAnchors(anchors.Keys);
            }

            // ---------------------------------------------------------
            // 1. ID
            // ---------------------------------------------------------
            var idResult = RunNeuronWithRetry(_idNeuron, analyzedBlocks, anchors, 0, usedBlocks);
            
            if (idResult.IsSuccess)
            {
                finalData.Id = idResult.Value;
                if (idResult.SourceBlock != null)
                {
                    usedBlocks.Add(idResult.SourceBlock);
                    anchors["ID"] = idResult.SourceBlock;
                }
                context.RegisterResult("id", idResult, "IdNeuron");
            }
            else 
            {
                finalData.Id = draftId;
                context.LogWarning("WARN_ID_NOT_FOUND", "ID could not be read. Using Draft/Zero.", "id");
            }

            // ---------------------------------------------------------
            // 2. POWER
            // ---------------------------------------------------------
            var powerAnchors = new Dictionary<string, AnalyzedBlock>(anchors);
            if (anchors.ContainsKey("PowerLabel")) powerAnchors["TargetLabel"] = anchors["PowerLabel"];

            var powerResult = RunNeuronWithRetry(_statsNeuron, analyzedBlocks, powerAnchors, 0, usedBlocks);
            finalData.Power = powerResult.Value;
            
            if (powerResult.SourceBlock != null) usedBlocks.Add(powerResult.SourceBlock);
            context.RegisterResult("power", powerResult, "StatsNeuron_Power");

            // ---------------------------------------------------------
            // 3. KILL POINTS
            // ---------------------------------------------------------
            var kpAnchors = new Dictionary<string, AnalyzedBlock>(anchors);
            if (anchors.ContainsKey("KpLabel")) kpAnchors["TargetLabel"] = anchors["KpLabel"];

            var kpNeuron = new StatsNeuron(requireBigNumber: true, excludeValue: finalData.Power);
            var kpResult = RunNeuronWithRetry(kpNeuron, analyzedBlocks, kpAnchors, 0, usedBlocks);
            finalData.KillPoints = kpResult.Value;
            
            if (kpResult.SourceBlock != null) usedBlocks.Add(kpResult.SourceBlock);
            context.RegisterResult("killPoints", kpResult, "StatsNeuron_KP");

            // ---------------------------------------------------------
            // 4. ALLIANCE (Tuple handling)
            // ---------------------------------------------------------
            var allianceResult = RunNeuronWithRetry(_allianceNeuron, analyzedBlocks, anchors, ("--", "--"), usedBlocks);
            finalData.AllianceTag = allianceResult.Value.Item1;
            finalData.AllianceName = allianceResult.Value.Item2;
            
            if (allianceResult.SourceBlock != null) usedBlocks.Add(allianceResult.SourceBlock);

            RegisterTupleField(context, "allianceTag", allianceResult.Value.Item1, allianceResult, "AllianceNeuron");
            RegisterTupleField(context, "allianceName", allianceResult.Value.Item2, allianceResult, "AllianceNeuron");

            // ---------------------------------------------------------
            // 5. CIVILIZATION
            // ---------------------------------------------------------
            var civResult = RunNeuronWithRetry(_civNeuron, analyzedBlocks, anchors, "--", usedBlocks);
            finalData.Civilization = civResult.Value;
            context.RegisterResult("civilization", civResult, "CivNeuron");

            // ---------------------------------------------------------
            // 6. NAME
            // ---------------------------------------------------------
            var nameResult = RunNeuronWithRetry(_nameNeuron, analyzedBlocks, anchors, "--", usedBlocks);
            finalData.Name = nameResult.Value;
            context.RegisterResult("name", nameResult, "NameNeuron");

            // ---------------------------------------------------------
            // PHASE 2: AUDIT AND DECISION
            // ---------------------------------------------------------
            AuditFinalData(finalData, context);

            bool isPerfect = finalData.Id > 0
                 && finalData.Power > 0
                 && finalData.Name != "--"
                 && finalData.Civilization != "--";

            // Stop cycle timer before decision
            context.StopTimer($"Cycle_{attempts + 1}");

            if (isPerfect)
            {
                context.Log("Cycle Audit: Perfect Match. Exiting loop.");
                break;
            }

            // ---------------------------------------------------------
            // PHASE 3: THE PARALLEL MAGNIFIER
            // ---------------------------------------------------------
            
            Task<List<OcrBlock>>? taskCiv = null;
            Task<List<OcrBlock>>? taskPower = null;
            Task<List<OcrBlock>>? taskName = null;
            
            bool scheduledTask = false;

            // Pass 'context' to Magnifier for attempt logging
            if (finalData.Civilization == "--")
            {
                var labelAnchor = anchors.ContainsKey("CivLabel") ? anchors["CivLabel"] : null;
                if (labelAnchor != null)
                {
                    context.Log("Scheduling Magnifier for: Civilization");
                    taskCiv = _magnifier.HuntForField(imagePath, labelAnchor, "Civilization", context);
                    scheduledTask = true;
                }
            }

            if (finalData.Power == 0)
            {
                var labelAnchor = anchors.ContainsKey("PowerLabel") ? anchors["PowerLabel"] : null;
                if (labelAnchor != null)
                {
                    context.Log("Scheduling Magnifier for: Power");
                    taskPower = _magnifier.HuntForField(imagePath, labelAnchor, "Power", context);
                    scheduledTask = true;
                }
            }

            if (finalData.Id > 0 && (finalData.Name == "--" || finalData.Name.Length < 3))
            {
                var idAnchor = anchors.ContainsKey("ID") ? anchors["ID"] : null;
                if (idAnchor != null)
                {
                    context.Log("Scheduling Magnifier for: Name");
                    taskName = _magnifier.HuntForField(imagePath, idAnchor, "Name", context);
                    scheduledTask = true;
                }
            }

            if (!scheduledTask) 
            {
                context.Log("No further magnification possible. Stopping.");
                keepTrying = false;
                continue;
            }

            // Timer for Magnifier wait
            context.StartTimer("MagnifierWait");
            
            var activeTasks = new List<Task<List<OcrBlock>>>();
            if (taskCiv != null) activeTasks.Add(taskCiv);
            if (taskPower != null) activeTasks.Add(taskPower);
            if (taskName != null) activeTasks.Add(taskName);

            await Task.WhenAll(activeTasks);
            
            context.StopTimer("MagnifierWait");

            // Process results
            bool foundNewInfo = false;

            if (taskCiv != null && taskCiv.Result.Any())
            {
                context.Log($"Magnifier found {taskCiv.Result.Count} blocks for Civ.");
                analyzedBlocks.AddRange(BlockClassifier.Classify(taskCiv.Result));
                foundNewInfo = true;
            }

            if (taskPower != null && taskPower.Result.Any())
            {
                context.Log($"Magnifier found {taskPower.Result.Count} blocks for Power.");
                analyzedBlocks.AddRange(BlockClassifier.Classify(taskPower.Result));
                foundNewInfo = true;
            }

            if (taskName != null && taskName.Result.Any())
            {
                context.Log($"Magnifier found {taskName.Result.Count} blocks for Name.");
                analyzedBlocks.AddRange(BlockClassifier.Classify(taskName.Result));
                foundNewInfo = true;
            }

            if (!foundNewInfo) keepTrying = false;

            attempts++;
        }

        context.StopTimer("TotalOrchestration");
        context.Log("Orchestration finished.");
        
        return (finalData, context);
    }

    // =================================================================================
    // HELPER METHODS
    // =================================================================================

    private void RegisterTupleField<T>(OcrAnalysisContext context, string key, string value, ExtractionResult<T> parentResult, string method)
    {
        var evidence = new FieldEvidenceDto
        {
            Value = value,
            Raw = parentResult.SourceBlock?.Raw.Text ?? "",
            Confidence = Math.Round(parentResult.Confidence, 2),
            Method = method,
            Box = parentResult.SourceBlock != null ? ExtractBox(parentResult.SourceBlock) : null
        };
        
        if (context.Evidence.ContainsKey(key)) context.Evidence[key] = evidence;
        else context.Evidence.Add(key, evidence);
    }

    private List<int>? ExtractBox(AnalyzedBlock block)
    {
         try
        {
            var rawBox = block.Raw.Box;
            int x = (int)rawBox[0][0];
            int y = (int)rawBox[0][1];
            int w = (int)(rawBox[1][0] - rawBox[0][0]);
            int h = (int)(rawBox[2][1] - rawBox[1][1]);
            return new List<int> { x, y, w, h };
        }
        catch { return null; }
    }

    private ExtractionResult<T> RunNeuronWithRetry<T>(
        IOcrNeuron<T> neuron,
        List<AnalyzedBlock> allBlocks,
        Dictionary<string, AnalyzedBlock> anchors,
        T defaultValue,
        HashSet<AnalyzedBlock> globalUsedBlocks)
    {
        var localBlacklist = new List<AnalyzedBlock>(globalUsedBlocks);
        ExtractionResult<T>? bestResult = null;
        int attempts = 0;

        while (attempts < 3)
        {
            var result = neuron.Process(allBlocks, anchors, localBlacklist);

            if (result.Confidence > 85) return result;

            if (bestResult == null || result.Confidence > bestResult.Confidence)
                bestResult = result;

            if (result.SourceBlock != null)
                localBlacklist.Add(result.SourceBlock);
            else
                break;

            attempts++;
        }

        return bestResult != null && bestResult.Confidence > 0
            ? bestResult
            : new ExtractionResult<T> { Value = defaultValue, Confidence = 0 };
    }

    private Dictionary<string, AnalyzedBlock> MapAnchors(List<AnalyzedBlock> blocks)
    {
        var anchors = new Dictionary<string, AnalyzedBlock>();
        void AddAnchor(string key, string[] keywords)
        {
            var match = blocks.FirstOrDefault(b => IsKeyword(b.Raw.Text, keywords));
            if (match != null) anchors[key] = match;
        }

        AddAnchor("AllianceLabel", RokVocabulary.AllianceLabels);
        AddAnchor("PowerLabel", RokVocabulary.PowerLabels);
        AddAnchor("KpLabel", RokVocabulary.KillPointsLabels);
        var civLabels = new[] { "Civilizacao", "Civilização", "Civilization", "Civilizacion" };
        AddAnchor("CivLabel", civLabels);

        return anchors;
    }

    private bool IsKeyword(string text, string[] keys)
    {
        foreach (var k in keys)
            if (RokCognitiveTools.CalculateSimilarity(text, k) > 0.82) return true;
        return false;
    }

    private void AuditFinalData(GovernorProfile data, OcrAnalysisContext context)
    {
        if (data.Power > 1_500_000_000)
        {
            context.LogWarning("WARN_IMPLAUSIBLE_POWER", $"Power ({data.Power}) seemed too high. Swapped with KP.", "power");
            var temp = data.Power;
            data.Power = data.KillPoints;
            data.KillPoints = temp;
        }

        if (data.KillPoints > data.Power && data.Power > 0)
        {
             context.Log("Note: KP is higher than Power. This is possible for T5 players but rare for new ones.");
        }

        if (string.IsNullOrWhiteSpace(data.Name)) data.Name = "--";
        if (string.IsNullOrWhiteSpace(data.AllianceTag)) data.AllianceTag = "--";
        if (string.IsNullOrWhiteSpace(data.AllianceName)) data.AllianceName = "--";

        bool hasId = data.Id > 0;
        bool hasContent = data.Name != "--" || data.Power > 0;

        data.IsSuccessfulRead = hasId && hasContent;

        if (!data.IsSuccessfulRead)
        {
            context.LogError("Audit Failed: Profile is incomplete (Missing ID or critical content).");
        }
    }
}
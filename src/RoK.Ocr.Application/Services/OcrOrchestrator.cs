using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RoK.Ocr.Application.Cognitive;
using RoK.Ocr.Application.Magnifier;
using RoK.Ocr.Application.Neurons;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Services;

public class OcrOrchestrator
{
    private readonly TheMagnifier _magnifier;

    // Specialist Neurons
    private readonly IdNeuron _idNeuron = new();
    private readonly NameNeuron _nameNeuron = new();
    private readonly AllianceNeuron _allianceNeuron = new();
    private readonly CivNeuron _civNeuron = new();
    private readonly StatsNeuron _statsNeuron = new(requireBigNumber: true);

    public OcrOrchestrator(TheMagnifier magnifier)
    {
        _magnifier = magnifier;
    }

    public async Task<GovernorProfile> AnalyzeAsync(string imagePath, List<OcrBlock> rawBlocks, int draftId = 0)
    {
        var finalData = new GovernorProfile();

        // 1. Initial Classification
        var analyzedBlocks = BlockClassifier.Classify(rawBlocks);

        int attempts = 0;
        bool keepTrying = true;

        // Retry Loop (Self-Correction Logic)
        while (keepTrying && attempts < 3)
        {
            // =================================================================
            // PHASE 1: NEURON EXECUTION (Fast, local processing)
            // =================================================================

            var usedBlocks = new HashSet<AnalyzedBlock>();
            var anchors = MapAnchors(analyzedBlocks);

            // 1. ID
            var idResult = RunNeuronWithRetry(_idNeuron, analyzedBlocks, anchors, 0, usedBlocks);
            if (idResult.IsSuccess)
            {
                finalData.Id = idResult.Value;
                if (idResult.SourceBlock != null)
                {
                    usedBlocks.Add(idResult.SourceBlock);
                    anchors["ID"] = idResult.SourceBlock;
                }
            }
            else 
            {
                finalData.Id = draftId;
            }

            // 2. POWER
            var powerAnchors = new Dictionary<string, AnalyzedBlock>(anchors);
            if (anchors.ContainsKey("PowerLabel")) powerAnchors["TargetLabel"] = anchors["PowerLabel"];

            var powerResult = RunNeuronWithRetry(_statsNeuron, analyzedBlocks, powerAnchors, 0, usedBlocks);
            finalData.Power = powerResult.Value;
            if (powerResult.SourceBlock != null) usedBlocks.Add(powerResult.SourceBlock);

            // 3. KILL POINTS
            var kpAnchors = new Dictionary<string, AnalyzedBlock>(anchors);
            if (anchors.ContainsKey("KpLabel")) kpAnchors["TargetLabel"] = anchors["KpLabel"];

            var kpNeuron = new StatsNeuron(requireBigNumber: true, excludeValue: finalData.Power);
            var kpResult = RunNeuronWithRetry(kpNeuron, analyzedBlocks, kpAnchors, 0, usedBlocks);
            finalData.KillPoints = kpResult.Value;
            if (kpResult.SourceBlock != null) usedBlocks.Add(kpResult.SourceBlock);

            // 4. ALLIANCE
            var allianceResult = RunNeuronWithRetry(_allianceNeuron, analyzedBlocks, anchors, ("--", "--"), usedBlocks);
            finalData.AllianceTag = allianceResult.Value.Item1;
            finalData.AllianceName = allianceResult.Value.Item2;
            if (allianceResult.SourceBlock != null) usedBlocks.Add(allianceResult.SourceBlock);

            // 5. CIVILIZATION
            var civResult = RunNeuronWithRetry(_civNeuron, analyzedBlocks, anchors, "--", usedBlocks);
            finalData.Civilization = civResult.Value;

            // 6. NAME
            var nameResult = RunNeuronWithRetry(_nameNeuron, analyzedBlocks, anchors, "--", usedBlocks);
            finalData.Name = nameResult.Value;

            // =================================================================
            // PHASE 2: AUDIT AND DECISION
            // =================================================================

            AuditFinalData(finalData);

            bool isPerfect = finalData.Id > 0
                 && finalData.Power > 0
                 && finalData.Name != "--"
                 && finalData.Civilization != "--";

            if (isPerfect)
            {
                // If everything is correct, exit the loop immediately.
                break;
            }

            // =================================================================
            // PHASE 3: THE PARALLEL MAGNIFIER (Turbo Mode 🚀)
            // =================================================================
            
            // Here we prepare the tasks, but we do NOT await them yet.
            Task<List<OcrBlock>>? taskCiv = null;
            Task<List<OcrBlock>>? taskPower = null;
            Task<List<OcrBlock>>? taskName = null;
            
            bool scheduledTask = false;

            // 1. Schedule Search for CIVILIZATION
            if (finalData.Civilization == "--")
            {
                var labelAnchor = anchors.ContainsKey("CivLabel") ? anchors["CivLabel"] : null;
                if (labelAnchor != null)
                {
                    // Fires the thread without waiting
                    taskCiv = _magnifier.HuntForField(imagePath, labelAnchor, "Civilization");
                    scheduledTask = true;
                }
            }

            // 2. Schedule Search for POWER
            if (finalData.Power == 0)
            {
                var labelAnchor = anchors.ContainsKey("PowerLabel") ? anchors["PowerLabel"] : null;
                if (labelAnchor != null)
                {
                    taskPower = _magnifier.HuntForField(imagePath, labelAnchor, "Power");
                    scheduledTask = true;
                }
            }

            // 3. Schedule Search for NAME
            if (finalData.Id > 0 && (finalData.Name == "--" || finalData.Name.Length < 3))
            {
                var idAnchor = anchors.ContainsKey("ID") ? anchors["ID"] : null;
                if (idAnchor != null)
                {
                    taskName = _magnifier.HuntForField(imagePath, idAnchor, "Name");
                    scheduledTask = true;
                }
            }

            // If no task was scheduled, there's nothing more to do.
            if (!scheduledTask) 
            {
                keepTrying = false;
                continue;
            }

            // --- PARALLELISM MOMENT ---
            // Collect all active tasks
            var activeTasks = new List<Task<List<OcrBlock>>>();
            if (taskCiv != null) activeTasks.Add(taskCiv);
            if (taskPower != null) activeTasks.Add(taskPower);
            if (taskName != null) activeTasks.Add(taskName);

            // C# waits here until ALL tasks are finished (in parallel)
            await Task.WhenAll(activeTasks);

            // --- RESULT PROCESSING ---
            bool foundNewInfo = false;

            // Process Civ result
            if (taskCiv != null && taskCiv.Result.Any())
            {
                analyzedBlocks.AddRange(BlockClassifier.Classify(taskCiv.Result));
                foundNewInfo = true;
            }

            // Process Power result
            if (taskPower != null && taskPower.Result.Any())
            {
                analyzedBlocks.AddRange(BlockClassifier.Classify(taskPower.Result));
                foundNewInfo = true;
            }

            // Process Name result
            if (taskName != null && taskName.Result.Any())
            {
                analyzedBlocks.AddRange(BlockClassifier.Classify(taskName.Result));
                foundNewInfo = true;
            }

            // If the Magnifier didn't find anything new, stop to avoid infinite loop
            if (!foundNewInfo) keepTrying = false;

            attempts++;
        }

        return finalData;
    }

    // =================================================================================
    // HELPER METHODS
    // =================================================================================

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

    private void AuditFinalData(GovernorProfile data)
    {
        if (data.Power > 1_500_000_000)
        {
            var temp = data.Power;
            data.Power = data.KillPoints;
            data.KillPoints = temp;
        }

        if (string.IsNullOrWhiteSpace(data.Name)) data.Name = "--";
        if (string.IsNullOrWhiteSpace(data.AllianceTag)) data.AllianceTag = "--";
        if (string.IsNullOrWhiteSpace(data.AllianceName)) data.AllianceName = "--";

        bool hasId = data.Id > 0;
        bool hasContent = data.Name != "--" || data.Power > 0;

        data.IsSuccessfulRead = hasId && hasContent;
    }
}
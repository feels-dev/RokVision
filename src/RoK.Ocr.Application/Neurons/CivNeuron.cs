using System;
using System.Collections.Generic;
using System.Linq;
using RoK.Ocr.Application.Cognitive;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Neurons;

public class CivNeuron : IOcrNeuron<string>
{
    public ExtractionResult<string> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        // 1. Look for blocks already classified as Civilization
        var civBlock = allBlocks
            .Except(blacklist)
            .FirstOrDefault(b => b.Type == BlockType.Civilization);

        if (civBlock != null)
        {
            return new ExtractionResult<string>
            {
                Value = CleanCivName(civBlock.Raw.Text),
                Confidence = 90,
                SourceBlock = civBlock
            };
        }

        return new ExtractionResult<string> { Value = "--", Confidence = 0 };
    }

    private string CleanCivName(string text)
    {
        foreach (var civ in RokVocabulary.CleanCivilizations)
        {
            if (RokCognitiveTools.CalculateSimilarity(text, civ) > 0.75) return civ;
            if (text.Contains(civ, StringComparison.InvariantCultureIgnoreCase)) return civ;
        }
        return text; // Returns original if no match (rare if it was typed as Civ)
    }
}
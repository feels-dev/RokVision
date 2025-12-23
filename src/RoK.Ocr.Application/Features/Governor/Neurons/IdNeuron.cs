using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Application.Common.Interfaces;

namespace RoK.Ocr.Application.Features.Governor.Neurons;

// Partial para suportar GeneratedRegex
public partial class IdNeuron : IOcrNeuron<int>
{
    // Regex Otimizado para limpeza do prefixo ID
    [GeneratedRegex(@"(ID|1D|lD|Id|id)\s*[:\)\.]?\s*", RegexOptions.Compiled)]
    private static partial Regex IdPrefixRegex();

    // Regex Otimizado para encontrar os dígitos
    [GeneratedRegex(@"\d{7,10}", RegexOptions.Compiled)]
    private static partial Regex IdDigitsRegex();

    public ExtractionResult<int> Process(List<AnalyzedBlock> allBlocks, Dictionary<string, AnalyzedBlock> anchors, List<AnalyzedBlock> blacklist)
    {
        // 1. Prioridade: Bloco já classificado como ID
        var idBlock = allBlocks
            .Except(blacklist)
            .FirstOrDefault(b => b.Type == BlockType.ID);

        if (idBlock != null)
        {
            return new ExtractionResult<int>
            {
                Value = ExtractId(idBlock.Raw.Text),
                Confidence = 95,
                SourceBlock = idBlock
            };
        }

        // 2. Fallback Inteligente
        var candidates = allBlocks
            .Except(blacklist)
            .Where(b => b.Type == BlockType.Unknown || b.Type == BlockType.Number)
            .Where(b => b.Center.Y < 600)
            .Select(b => new { Val = ExtractId(b.Raw.Text), Block = b })
            .Where(x => x.Val > 1_000_000 && x.Val < 2_000_000_000)
            .OrderByDescending(x => x.Block.Raw.Text.Contains("ID", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (candidates != null)
        {
            return new ExtractionResult<int>
            {
                Value = candidates.Val,
                Confidence = 70,
                SourceBlock = candidates.Block
            };
        }

        return new ExtractionResult<int> { Value = 0, Confidence = 0 };
    }

    private int ExtractId(string text)
    {
        // Usa os Regex compilados
        string clean = IdPrefixRegex().Replace(text, "");
        var digits = IdDigitsRegex().Match(clean);
        
        if (digits.Success && int.TryParse(digits.Value, out int id))
        {
            return id;
        }
        return 0;
    }
}
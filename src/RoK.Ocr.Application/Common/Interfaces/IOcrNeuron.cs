using System.Collections.Generic;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Common.Interfaces;

public interface IOcrNeuron<T>
{
    ExtractionResult<T> Process(
        List<AnalyzedBlock> allBlocks, 
        Dictionary<string, AnalyzedBlock> anchors, 
        List<AnalyzedBlock> blacklist
    );
}
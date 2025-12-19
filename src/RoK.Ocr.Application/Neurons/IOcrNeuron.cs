using System.Collections.Generic;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Neurons;

public interface IOcrNeuron<T>
{
    ExtractionResult<T> Process(
        List<AnalyzedBlock> allBlocks, 
        Dictionary<string, AnalyzedBlock> anchors, 
        List<AnalyzedBlock> blacklist
    );
}
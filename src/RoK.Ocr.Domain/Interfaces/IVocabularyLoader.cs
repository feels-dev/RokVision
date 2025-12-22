using RoK.Ocr.Domain.Models.Reports;
using System.Collections.Generic;

namespace RoK.Ocr.Domain.Interfaces;

public interface IVocabularyLoader
{
    List<CommanderEntry> GetCommanders();
    List<CommanderEntry> GetNpcs();
}
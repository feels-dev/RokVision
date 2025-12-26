using System.Collections.Generic;

namespace RoK.Ocr.Domain.Constants;

public static class XpVocabulary
{
    public static readonly List<(int XpValue, string Id, string[] Colors)> TargetBooks = new()
    {
        (100,    "XP_100",   new[] { "Blue", "Purple" }),
        (500,    "XP_500",   new[] { "Blue", "Purple" }),
        (1000,   "XP_1000",  new[] { "Purple", "Blue" }),
        (5000,   "XP_5000",  new[] { "Purple", "Blue" }),
        (10000,  "XP_10000", new[] { "Gold", "Purple", "Blue" }),
        // ADICIONADO 'Blue' AQUI:
        (20000,  "XP_20000", new[] { "Gold", "Blue" }),
        (50000,  "XP_50000", new[] { "Gold", "Blue" })
    };
}
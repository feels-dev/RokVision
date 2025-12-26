using System.Collections.Generic;

namespace RoK.Ocr.Domain.Constants;

public static class ApVocabulary
{
    // Items recognized by the system
    public static readonly List<(string Name, int Value, string Id)> TargetItems = new()
    {
        ("Emergency Action Point Recovery", 50,   "AP_50"),
        ("Basic Action Point Recovery",     100,  "AP_100"),
        ("Intermediate Action Point Recovery", 500,  "AP_500"),
        ("Advanced Action Point Recovery",  1000, "AP_1000")
    };

    // Keywords to identify ownership/quantity (Multi-language support)
    public static readonly string[] OwnershipKeywords = 
    { 
        "Own", "Owned", "Possui", "Possuído", "Possiede", "Possédés",
        "Eigener", "Possédé", "Im Besitz"
    };
}
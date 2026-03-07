using System;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Constants;

namespace RoK.Ocr.Application.Features.Map.Neurons;

/// <summary>
/// Result object containing the extracted City Name, Alliance Tag, and Validation Status.
/// </summary>
public record CityParseResult(string Name, string AllianceTag, string RejectReason)
{
    public bool IsValid => Name != "--INVALID--";
}

/// <summary>
/// Neuron specialized in parsing City Names and Alliance Tags from OCR text blocks.
/// Uses heuristics based on common patterns observed in the game UI.
/// </summary>
public class CityNeuron
{
    /// <summary>
    /// Parses the raw text into a City Name and Alliance Tag.
    /// Returns a CityParseResult containing the validation state.
    /// </summary>
    public CityParseResult Parse(string rawText, bool hasShield)
    {
        if (string.IsNullOrWhiteSpace(rawText) || rawText.Length < 2)
            return new CityParseResult("--INVALID--", "", "EMPTY_OR_TOO_SHORT");

        // STEP 1: PRE-PROCESSING SANITIZATION
        string text = SanitizeInput(rawText);

        // STEP 2: NOISE FILTERS
        if (IsEventTimerOrNoise(text))
            return new CityParseResult("--INVALID--", "", "MATCHED_TIMER_PATTERN");

        if (text.All(char.IsDigit))
            return new CityParseResult("--INVALID--", "", "ONLY_DIGITS");

        // Calculate letter density to avoid reading random map textures as text
        int validChars = text.Count(c => char.IsLetterOrDigit(c) || c == ' ' || c == '[' || c == ']');
        double letterRatio = (double)validChars / (double)text.Length;
        if (letterRatio < 0.6)
            return new CityParseResult("--INVALID--", "", $"LOW_LETTER_RATIO_{Math.Round(letterRatio, 2)}");

        // STEP 3: TAG EXTRACTION LOGIC
        string tag = "";
        string name = "";

        int openIdx = text.IndexOf('[');
        int closeIdx = text.IndexOf(']');

        if (openIdx >= 0 && closeIdx > openIdx)
        {
            tag = text.Substring(openIdx + 1, closeIdx - openIdx - 1);
            name = text.Substring(closeIdx + 1);
        }
        else if (openIdx >= 0 && closeIdx == -1)
        {
            string afterOpen = text.Substring(openIdx + 1).TrimStart();
            int spaceIdx = afterOpen.IndexOf(' ');

            if (spaceIdx > 2 && spaceIdx <= 5)
            {
                tag = afterOpen.Substring(0, spaceIdx);
                name = afterOpen.Substring(spaceIdx + 1);
            }
            else
            {
                int tagLen = Math.Min(4, afterOpen.Length);
                tag = afterOpen.Substring(0, tagLen);
                name = afterOpen.Substring(tagLen);
            }
        }
        else if (closeIdx >= 0 && openIdx == -1)
        {
            string beforeClose = text.Substring(0, closeIdx).TrimEnd();
            int spaceIdx = beforeClose.LastIndexOf(' ');

            if (spaceIdx >= 0)
            {
                tag = beforeClose.Substring(spaceIdx + 1);
                name = text.Substring(closeIdx + 1);
            }
            else
            {
                int tagLen = Math.Min(4, beforeClose.Length);
                tag = beforeClose.Substring(Math.Max(0, beforeClose.Length - tagLen));
                name = text.Substring(closeIdx + 1);
            }
        }
        else
        {
            tag = "";
            name = text;
        }

        // STEP 4: FINAL CLEANUP
        tag = CleanTag(tag);
        name = CleanName(name);

        // STEP 5: SMART BLOCKLIST VALIDATION
        // If the name is a system-generated default name (e.g., "Governor12345"), 
        // we bypass the vocabulary blocklist completely.
        if (!IsDefaultGovernorName(name))
        {
            string? matchedBlocklistWord = GetUiNoiseMatch(name);
            if (!string.IsNullOrEmpty(matchedBlocklistWord))
                return new CityParseResult("--INVALID--", "", $"BLOCKED_BY_VOCABULARY_MATCH: '{matchedBlocklistWord}'");
        }

        if (name.Length < 2)
            return new CityParseResult("--INVALID--", "", "NAME_TOO_SHORT_AFTER_CLEANUP");

        return new CityParseResult(name, tag, "SUCCESS");
    }

    private string CleanTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return "";
        tag = tag.Trim();
        if (tag.Length > 5) return "";
        return tag;
    }

    private string SanitizeInput(string text)
    {
        text = text.Trim();
        // Fix common bracket OCR errors: 1 ->[, ] -> I, etc.
        text = Regex.Replace(text, @"(\[[A-Za-z0-9]{4,5})([1Il])", "$1]");
        // Remove special characters that are not part of names
        text = Regex.Replace(text, @"[^a-zA-Z0-9\p{L}\[\]\s]", "");
        return text.Trim();
    }

    private bool IsEventTimerOrNoise(string text)
    {
        // Pattern for event timers: (d) (h):(m):(s)
        if (Regex.IsMatch(text, @"(\d+d)?\s*\d{1,2}:\d{2}:\d{2}")) return true;
        // Filter for ratios like "1/3"
        if (text.Contains('/') && text.Length < 6 && text.Any(char.IsDigit)) return true;
        return false;
    }

    private string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        name = name.Trim();
        // Remove trailing numbers separated by space (e.g., city levels like " 13")
        name = Regex.Replace(name, @"\s+\d{1,2}$", "").Trim();
        return name;
    }

    /// <summary>
    /// Checks if the name matches the Rise of Kingdoms default player name pattern.
    /// Dynamically builds the Regex based on RokVocabulary to support multi-language.
    /// E.g., "Governor12345", "Governador 8976".
    /// </summary>
    private bool IsDefaultGovernorName(string name)
    {
        // Dynamically join prefixes with OR operator (|)
        // Escaping is generally not needed for simple letters, but good practice if prefixes had special chars.
        string prefixes = string.Join("|", RokVocabulary.DefaultGovernorPrefixes);

        // Regex Construction:
        // ^(...)     - Starts with one of the vocabulary prefixes
        // \s*        - Optional space
        // \d{4,}     - Followed by at least 4 digits
        // $          - End of string
        string pattern = $@"^({prefixes})\s*\d{{4,}}$";

        return Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase);
    }

    private string? GetUiNoiseMatch(string text)
    {
        foreach (var keyword in RokVocabulary.MapUiBlocklist)
        {
            // Exact match = Instant Block
            if (text.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                return keyword;

            // Partial match logic:
            // We only block if the forbidden word represents the vast majority of the text (> 80%).
            // This prevents false positives when a player's actual name contains a blocked substring.
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                double matchRatio = (double)keyword.Length / text.Length;
                if (matchRatio > 0.8)
                    return keyword;
            }
        }
        return null;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Application.Common.Cognitive;
using RoK.Ocr.Domain.Constants;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Application.Features.Map.Cognitive;

/// <summary>
/// Specialized classifier for Map screenshot blocks.
/// Follows the same pattern as WarBlockClassifier.
/// </summary>
public static partial class MapBlockClassifier
{
    // ═══════════════════════════════════════════════════════════
    // COMPILED REGEX (Performance Optimized)
    // ═══════════════════════════════════════════════════════════
    
    [GeneratedRegex(@"^\[?[a-zA-Z0-9]{3,4}\]?", RegexOptions.Compiled)]
    private static partial Regex AllianceTagRegex();
    
    [GeneratedRegex(@"#\d{4}.*X:?\s*\d+.*Y:?\s*\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CoordinateRegex();
    
    [GeneratedRegex(@"^\d+[KM]?$", RegexOptions.Compiled)]
    private static partial Regex PureNumberRegex();
    
    [GeneratedRegex(@"\d{2}:\d{2}|UTC|VIP", RegexOptions.Compiled)]
    private static partial Regex HudMetadataRegex();

    // ═══════════════════════════════════════════════════════════
    // MAIN CLASSIFIER
    // ═══════════════════════════════════════════════════════════
    
    public static List<AnalyzedBlock> Classify(List<OcrBlock> rawBlocks)
    {
        var analyzedBlocks = rawBlocks.Select(b => new AnalyzedBlock { Raw = b }).ToList();
        
        foreach (var block in analyzedBlocks)
        {
            string text = block.Raw.Text.Trim();
            
            // PRIORITY 1: Coordinates (unique pattern)
            if (CoordinateRegex().IsMatch(text))
            {
                block.Type = BlockType.Coordinate;
            }
            
            // PRIORITY 2: Alliance Tags ([TAG] or TAG])
            else if (AllianceTagRegex().IsMatch(text) && text.Length >= 3 && text.Length <= 8)
            {
                block.Type = BlockType.Tag;
            }
            
            // PRIORITY 3: City Names (heuristic)
            else if (LooksLikeCityName(text))
            {
                block.Type = BlockType.Unknown; // Will be filtered spatially later
            }
            
            // PRIORITY 4: Pure Numbers
            else if (IsPureNumeric(text))
            {
                block.Type = BlockType.Number;
            }
            
            // PRIORITY 5: HUD Metadata (time, VIP, resources)
            else if (HudMetadataRegex().IsMatch(text))
            {
                block.Type = BlockType.UI;
            }
            
            // PRIORITY 6: UI Blocklist
            else if (IsUiGarbage(text))
            {
                block.Type = BlockType.UI;
            }
            
            // DEFAULT: Unknown (will be spatially filtered)
            else
            {
                block.Type = BlockType.Unknown;
            }
        }
        
        return analyzedBlocks;
    }

    // ═══════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════
    
    private static bool LooksLikeCityName(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3) 
            return false;
        
        // Valid city names:
        // - [TAG]Name or Name [TAG] (already caught as Tag above)
        // - Pure text with letters (3+ chars)
        // - Can have spaces (e.g., "DD Feels")
        
        // Reject if:
        // - All numbers
        if (text.All(char.IsDigit)) return false;
        
        // - Contains coordinates pattern
        if (text.Contains("X:") || text.Contains("Y:")) return false;
        
        // - Is a level number (e.g., "23" alone)
        if (text.Length <= 2 && text.All(char.IsDigit)) return false;
        
        // - Has excessive special chars (OCR noise)
        int specialChars = text.Count(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '[' && c != ']');
        if (specialChars > 3) return false;
        
        return true;
    }
    
    private static bool IsPureNumeric(string text)
    {
        string clean = text.Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
        return PureNumberRegex().IsMatch(clean);
    }
    
    private static bool IsUiGarbage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        
        // Check against RoK vocabulary blocklist
        foreach (var keyword in RokVocabulary.MapUiBlocklist)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
            
            // Fuzzy match for OCR typos
            if (RokCognitiveTools.CalculateSimilarity(text, keyword) > 0.82)
                return true;
        }
        
        return false;
    }
}
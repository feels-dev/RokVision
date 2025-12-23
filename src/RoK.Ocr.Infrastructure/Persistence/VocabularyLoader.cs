using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models.Reports; // Para CommanderEntry

namespace RoK.Ocr.Infrastructure.Persistence;

public class VocabularyLoader : IVocabularyLoader
{
    private List<CommanderEntry> _commanders = new();
    private List<CommanderEntry> _npcs = new(); // Cache for NPCs
    
    private readonly string _commandersPath;
    private readonly string _npcsPath;
    private readonly ILogger<VocabularyLoader> _logger;

    public VocabularyLoader(string rootPath, ILogger<VocabularyLoader> logger)
    {
        _logger = logger;
        // Defines the paths for both files (Assumes Assets folder is in project root)
        _commandersPath = Path.Combine(rootPath, "Assets", "CommandersVocabulary.json");
        _npcsPath = Path.Combine(rootPath, "Assets", "NpcsVocabulary.json");
    }

    public List<CommanderEntry> GetCommanders()
    {
        if (_commanders.Any()) return _commanders;
        _commanders = LoadFile(_commandersPath, "Commanders");
        return _commanders;
    }

    public List<CommanderEntry> GetNpcs()
    {
        if (_npcs.Any()) return _npcs;
        _npcs = LoadFile(_npcsPath, "NPCs/Bosses");
        return _npcs;
    }

    // Helper method to avoid code repetition
    private List<CommanderEntry> LoadFile(string path, string label)
    {
        try
        {
            if (!File.Exists(path))
            {
                _logger.LogCritical("JSON for {Label} not found at: {Path}", label, path);
                return new List<CommanderEntry>();
            }

            string json = File.ReadAllText(path);
            var result = JsonSerializer.Deserialize<List<CommanderEntry>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<CommanderEntry>();

            _logger.LogInformation("{Label} vocabulary loaded: {Count} entries available.", label, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load JSON for {Label}", label);
            return new List<CommanderEntry>();
        }
    }
}
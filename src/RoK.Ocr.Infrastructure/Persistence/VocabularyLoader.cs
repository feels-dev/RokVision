using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;          // For NpcEntry
using RoK.Ocr.Domain.Models.Reports;  // For CommanderEntry

namespace RoK.Ocr.Infrastructure.Persistence;

public class VocabularyLoader : IVocabularyLoader
{
    private List<CommanderEntry> _commanders = new();
    private List<NpcEntry> _npcs = new(); // Changed type to NpcEntry

    private readonly string _commandersPath;
    private readonly string _npcsPath;
    private readonly ILogger<VocabularyLoader> _logger;

    public VocabularyLoader(string rootPath, ILogger<VocabularyLoader> logger)
    {
        _logger = logger;
        // Adjust path combination to ensure it works in Docker/Linux environments
        // Assumes "Assets" is adjacent to the running binary or mapped correctly
        _commandersPath = Path.Combine(rootPath, "Assets", "CommandersVocabulary.json");
        _npcsPath = Path.Combine(rootPath, "Assets", "NpcsVocabulary.json");
    }

    public List<CommanderEntry> GetCommanders()
    {
        if (_commanders.Any()) return _commanders;
        _commanders = LoadFile<CommanderEntry>(_commandersPath, "Commanders");
        return _commanders;
    }

    public List<NpcEntry> GetNpcs()
    {
        if (_npcs.Any()) return _npcs;
        // Generic call maps the JSON fields to NpcEntry properties automatically
        _npcs = LoadFile<NpcEntry>(_npcsPath, "NPCs/Bosses");
        return _npcs;
    }

    // Generic Helper Method <T>
    private List<T> LoadFile<T>(string path, string label)
    {
        try
        {
            if (!File.Exists(path))
            {
                // Try looking in current directory if root path fails (fallback)
                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", Path.GetFileName(path));
                if (File.Exists(localPath)) path = localPath;
                else
                {
                    _logger.LogCritical("JSON for {Label} not found at: {Path}", label, path);
                    return new List<T>();
                }
            }

            string json = File.ReadAllText(path);
            var result = JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<T>();

            _logger.LogInformation("{Label} vocabulary loaded: {Count} entries available.", label, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load JSON for {Label}", label);
            return new List<T>();
        }
    }
}
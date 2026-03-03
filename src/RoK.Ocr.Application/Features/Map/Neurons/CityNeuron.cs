using System;
using System.Linq;
using System.Text.RegularExpressions;
using RoK.Ocr.Domain.Constants;

namespace RoK.Ocr.Application.Features.Map.Neurons;

/// <summary>
/// Neuron specialized in parsing City Names and Alliance Tags from OCR text blocks.
/// Uses heuristics based on common patterns observed in the game UI.
/// </summary>
public class CityNeuron
{
    public (string Name, string AllianceTag) Parse(string rawText, bool hasShield)
    {
        if (string.IsNullOrWhiteSpace(rawText) || rawText.Length < 2)
            return ("--INVALID--", "");

        // ETAPA 1: SANITIZAÇÃO PRÉ-PROCESSAMENTO
        // Lida com erros comuns de OCR antes de analisar.
        // Ex: "1Ab461DDF" -> "[Ab46]DDF",  "[Ab461DDF" -> "[Ab46]DDF"
        string text = SanitizeInput(rawText);

        // ETAPA 2: FILTROS DE RUÍDO
        // Rejeita timers, números soltos, e texto com baixa densidade de letras.
        if (IsEventTimerOrNoise(text)) return ("--INVALID--", "");
        if (text.All(char.IsDigit)) return ("--INVALID--", "");

        int validChars = text.Count(c => char.IsLetterOrDigit(c) || c == ' ' || c == '[' || c == ']');
        if ((double)validChars / text.Length < 0.7) return ("--INVALID--", "");

        // ETAPA 3: LÓGICA DE EXTRAÇÃO DE TAG (Mantida)
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

        // ETAPA 4: LIMPEZA FINAL
        tag = CleanTag(tag);
        name = CleanName(name);

        if (IsUiNoise(name) || name.Length < 2)
            return ("--INVALID--", "");

        return (name, tag);
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

        // 1. Corrige erros de colchetes: 1 -> [, ] -> I, etc.
        // Ex: "[Ab461DDF" -> "[Ab46]DDF"
        // Regex procura por 4-5 chars alfanuméricos após '[' e um número/letra maiúscula
        text = Regex.Replace(text, @"(\[[A-Za-z0-9]{4,5})([1Il])", "$1]");

        // 2. Remove caracteres especiais que não fazem parte de nomes.
        // Mantém letras (incluindo acentos e alfabetos estrangeiros), números, e colchetes.
        text = Regex.Replace(text, @"[^a-zA-Z0-9\p{L}\[\]\s]", "");

        return text.Trim();
    }

    private bool IsEventTimerOrNoise(string text)
    {
        // Padrão para timers de evento: (d) (h):(m):(s)
        // Ex: "9d 09:40:55", "29d21:36:16"
        // Regex procura por "d", "h", "m" ou "s" junto com múltiplos ":" e números.
        if (Regex.IsMatch(text, @"(\d+d)?\s*\d{1,2}:\d{2}:\d{2}"))
            return true;

        // Filtro para ratios como "1/3" que podem ser lidos por engano
        if (text.Contains('/') && text.Length < 6 && text.Any(char.IsDigit))
            return true;

        return false;
    }

    private string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        name = name.Trim();

        name = Regex.Replace(name, @"\s+\d{1,2}$", "").Trim();

        return name;
    }

    private bool IsUiNoise(string text)
    {
        // Consome diretamente do Vocabulary centralizado
        foreach (var keyword in RokVocabulary.MapUiBlocklist)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
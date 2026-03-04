using System;
using System.Collections.Generic;
using System.Linq;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Constants;
using SixLabors.ImageSharp;

namespace RoK.Ocr.Application.Common.Cognitive;

public class DynamicHudMask
{
    public List<Rectangle> BlockedZones { get; set; } = new();

    public bool IsSafe(int x, int y)
    {
        return !BlockedZones.Any(z => z.Contains(x, y));
    }
}

public static class DynamicHudLocator
{
    public static DynamicHudMask BuildDynamicMask(List<AnalyzedBlock> allBlocks, int imgWidth, int imgHeight)
    {
        var mask = new DynamicHudMask();

        foreach (var block in allBlocks)
        {
            string text = block.Raw.Text.Trim();

            // Só processamos interface
            if (block.Type != BlockType.UI && block.Type != BlockType.Number && !IsUiAnchor(text)) 
                continue;

            int bx = (int)block.Raw.Box[0][0];
            int by = (int)block.Raw.Box[0][1];
            int bw = (int)(block.Raw.Box[1][0] - block.Raw.Box[0][0]);
            int bh = (int)(block.Raw.Box[2][1] - block.Raw.Box[1][1]);
            int cx = bx + (bw / 2);
            int cy = by + (bh / 2);

            // =================================================================
            // ZONA 1: CHAT & COMANDOS (Canto Inferior Esquerdo)
            // =================================================================
            if (cx < imgWidth * 0.40 && cy > imgHeight * 0.60)
            {
                int topPadding = HasMatch(text, RokVocabulary.ChatKeywords) ? 140 : 60;
                mask.BlockedZones.Add(new Rectangle(
                    0, // Parede Esquerda
                    Math.Max(0, by - topPadding), 
                    bx + bw + 50, 
                    imgHeight // Chão
                ));
            }
            // =================================================================
            // ZONA 2: PERFIL & COORDENADAS (Canto Superior Esquerdo)
            // =================================================================
            else if (cx < imgWidth * 0.40 && cy < imgHeight * 0.20)
            {
                // Se for a caixa de coordenadas ("#3379 X:...") estica mais pra direita
                int rightPadding = text.Contains("X:") || text.Contains("Y:") ? 150 : 60;
                mask.BlockedZones.Add(new Rectangle(
                    0, // Parede Esquerda
                    0, // Teto
                    bx + bw + rightPadding, 
                    by + bh + 40
                ));
            }
            // =================================================================
            // ZONA 3: RECURSOS & EVENTOS (Canto Superior Direito)
            // =================================================================
            else if (cx >= imgWidth * 0.40 && cy < imgHeight * 0.20)
            {
                // Força colar no teto e na PAREDE DIREITA
                mask.BlockedZones.Add(new Rectangle(
                    Math.Max(0, bx - 40), 
                    0, // Teto
                    imgWidth, // Estica até o fim da tela na direita
                    by + bh + 30
                ));
            }
            // =================================================================
            // ZONA 4: MENU PRINCIPAL (Canto Inferior Direito)
            // =================================================================
            else if (cx >= imgWidth * 0.40 && cy > imgHeight * 0.75)
            {
                // Força colar no chão e na PAREDE DIREITA
                mask.BlockedZones.Add(new Rectangle(
                    Math.Max(0, bx - 40), 
                    Math.Max(0, by - 60), // Puxa pra cima pros botões redondos
                    imgWidth, // Parede Direita
                    imgHeight // Chão
                ));
            }
            // =================================================================
            // ZONA 5: MARCHA NO MAPA (Meio-Direita)
            // =================================================================
            else if (cx > imgWidth * 0.85 && cy > imgHeight * 0.20 && cy < imgHeight * 0.75)
            {
                // Cola na PAREDE DIREITA
                mask.BlockedZones.Add(new Rectangle(
                    Math.Max(0, bx - 60), // Margem pra pegar as fotos das comandantes
                    Math.Max(0, by - 40), 
                    imgWidth, // Parede Direita
                    bh + 80
                ));
            }
            // =================================================================
            // ZONA 6: MENUS RETRÁTEIS (Meio-Esquerda)
            // =================================================================
            else if (cx < imgWidth * 0.15 && cy > imgHeight * 0.20 && cy < imgHeight * 0.60)
            {
                // Cola na PAREDE ESQUERDA
                mask.BlockedZones.Add(new Rectangle(
                    0, // Parede Esquerda
                    Math.Max(0, by - 30), 
                    bx + bw + 40, 
                    bh + 60
                ));
            }
        }

        // Zonas Mortas Globais (Teto e Chão básicos)
        mask.BlockedZones.Add(new Rectangle(0, 0, imgWidth, (int)(imgHeight * 0.05))); 
        mask.BlockedZones.Add(new Rectangle(0, imgHeight - (int)(imgHeight * 0.03), imgWidth, (int)(imgHeight * 0.03)));

        return mask;
    }

    private static bool IsUiAnchor(string text)
    {
        return HasMatch(text, RokVocabulary.TopUiAnchors) || 
               HasMatch(text, RokVocabulary.BottomUiAnchors) || 
               HasMatch(text, RokVocabulary.ChatKeywords) ||
               HasMatch(text, RokVocabulary.MapUiBlocklist) ||
               text.Contains("VIP", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMatch(string text, string[] vocabulary)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        
        foreach (var word in vocabulary)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
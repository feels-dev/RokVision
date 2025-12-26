using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Domain.Models.Experience;

namespace RoK.Ocr.Application.Features.Experience.Services;

public class XpMagnifier
{
    private readonly IOcrService _ocrService;
    private readonly ILogger<XpMagnifier> _logger;

    public XpMagnifier(IOcrService ocrService, ILogger<XpMagnifier> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task ResolveMissingQuantitiesAsync(string imagePath, List<XpItemEntry> incompleteItems)
    {
        var targets = incompleteItems.Where(i => i.Quantity == -1 && i.AnchorBlock != null).ToList();
        if (!targets.Any()) return;

        // --- DINAMISMO: CÁLCULO DA RÉGUA LOCAL ---
        // Calcula a altura mediana de todas as âncoras pendentes para normalizar o recorte.
        var heights = targets.Select(t => t.AnchorBlock!.Raw.Box[2][1] - t.AnchorBlock!.Raw.Box[0][1]).OrderBy(h => h).ToList();
        double medianH = heights.Count > 0 ? heights[heights.Count / 2] : 20.0; // Fallback seguro

        var requestMap = new Dictionary<string, XpItemEntry>();
        var batchRequests = new List<(string Id, int[] Box, string Strategy)>();

        foreach (var item in targets)
        {
            var box = item.AnchorBlock!.Raw.Box;
            
            // Centro da Âncora
            double centerX = (box[0][0] + box[2][0]) / 2;
            double bottomY = box[2][1];

            // --- GEOMETRIA ADAPTATIVA (Baseada na Média) ---
            // Ignora a largura/altura específica deste bloco (que pode ser ruim).
            // Usa a Média Global (medianH) para desenhar a caixa ideal.

            // Y: Começa levemente acima do fim do texto (overlap)
            int cropY = (int)(bottomY - (medianH * 0.2));
            
            // Altura Fixa Proporcional: 4x a altura da letra
            int cropH = (int)(medianH * 4.5);

            // SHOT 1: Centralizado (Wide)
            // Largura: 8x a altura da letra (cobre a largura do ícone)
            int cropW_1 = (int)(medianH * 10.0);
            int cropX_1 = (int)(centerX - (cropW_1 / 2));

            // SHOT 2: Foco Direita (Para números em "1.000")
            // Começa no centro e vai pra direita
            int cropW_2 = (int)(medianH * 6.0);
            int cropX_2 = (int)(centerX); 

            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            requestMap[id1] = item;
            requestMap[id2] = item;

            // Envia WhiteIsolation (Melhor para ícones coloridos)
            batchRequests.Add((id1, new[] { cropX_1, cropY, cropW_1, cropH }, "WhiteIsolation"));
            batchRequests.Add((id2, new[] { cropX_2, cropY, cropW_2, cropH }, "WhiteIsolation"));
        }

        _logger.LogInformation("[XpMagnifier] Disparando {Count} rescans adaptativos.", batchRequests.Count);

        var results = await _ocrService.AnalyzeBatchAsync(imagePath, batchRequests);

        foreach (var res in results)
        {
            if (requestMap.TryGetValue(res.CustomId, out var item))
            {
                string clean = new string(res.Text.Where(char.IsDigit).ToArray());
                
                if (int.TryParse(clean, out int qty) && qty > 0)
                {
                    // Lógica de Confiança
                    double threshold = qty < 10 ? 0.15 : 0.40;

                    if (res.Confidence > threshold)
                    {
                        double newConf = Math.Round(res.Confidence * 100, 2);

                        // Prioridade: Maior Valor > Maior Confiança
                        if (item.Quantity == -1)
                        {
                            item.Quantity = qty;
                            item.Confidence = newConf;
                            item.DetectedColor = item.DetectedColor.Replace("_PENDING", "");
                        }
                        else if (qty > item.Quantity)
                        {
                            item.Quantity = qty;
                            item.Confidence = newConf;
                        }
                        else if (qty == item.Quantity && newConf > item.Confidence)
                        {
                            item.Confidence = newConf;
                        }
                    }
                }
            }
        }
    }
}
// using Microsoft.AspNetCore.Mvc;
// using RoK.Ocr.Domain.Interfaces;
// using RoK.Ocr.Application.Common.Cognitive;
// using RoK.Ocr.Application.Features.Map.Cognitive;
// using SixLabors.ImageSharp;
// using SixLabors.ImageSharp.Processing;
// using SixLabors.ImageSharp.Drawing.Processing;
// using SixLabors.ImageSharp.PixelFormats;
// using System.Diagnostics;

// namespace RoK.Ocr.Api.Controllers;

// [ApiController]
// [Route("api/debug")]
// public class DebugController : ControllerBase
// {
//     private readonly IImageStorage _storage;
//     private readonly IOcrService _ocrService;

//     public DebugController(IImageStorage storage, IOcrService ocrService)
//     {
//         _storage = storage;
//         _ocrService = ocrService;
//     }

//     [HttpPost("test-hud")]
//     [Consumes("multipart/form-data")]
//     public async Task<IActionResult> TestDynamicHud(IFormFile image)
//     {
//         if (image == null || image.Length == 0) return BadRequest("Nenhuma imagem enviada.");

//         var sw = Stopwatch.StartNew();
//         string baseDir = _storage.GetBasePath();
//         string debugDir = Path.Combine(baseDir, "uploads", "debug_hud");
//         if (!Directory.Exists(debugDir)) Directory.CreateDirectory(debugDir);

//         try
//         {
//             string originalPath = Path.Combine(debugDir, $"orig_{image.FileName}");
//             using (var stream = image.OpenReadStream())
//             {
//                 using var fileStream = new FileStream(originalPath, FileMode.Create);
//                 await stream.CopyToAsync(fileStream);
//             }

//             var imgInfo = await Image.IdentifyAsync(originalPath);
//             int imgWidth = imgInfo.Width;
//             int imgHeight = imgInfo.Height;

//             byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(originalPath);
//             var ocrResult = await _ocrService.AnalyzeImageAsync(originalPath, fileBytes);

//             if (ocrResult.Blocks == null || !ocrResult.Blocks.Any())
//                 return BadRequest("O OCR não detectou nenhum texto na imagem.");

//             var analyzedBlocks = MapBlockClassifier.Classify(ocrResult.Blocks);

//             // A MÁGICA: Pega as zonas bloqueadas (que formam o contorno)
//             var hudMask = DynamicHudLocator.BuildDynamicMask(analyzedBlocks, imgWidth, imgHeight);

//             // 1. IMAGEM HIGHLIGHT (Para Calibragem Visual)
//             // Vamos pintar a HUD de Vermelho Translúcido com borda Vermelha Forte
//             string drawnPath = Path.Combine(debugDir, $"HUD_CONTOUR_{Guid.NewGuid().ToString()[..6]}.jpg");
//             using (var imgDrawn = await Image.LoadAsync(originalPath))
//             {
//                 imgDrawn.Mutate(ctx =>
//                 {
//                     // Define a cor de preenchimento (Vermelho 40% transparente)
//                     var fillColor = Color.Red.WithAlpha(0.4f);

//                     foreach (var zone in hudMask.BlockedZones)
//                     {
//                         // Desenha o bloco translúcido
//                         ctx.Fill(fillColor, zone);
//                         // Desenha a linha de borda forte (para vermos os recortes)
//                         ctx.DrawPolygon(Color.Red, 2f, new PointF[] {
//                             new PointF(zone.Left, zone.Top), new PointF(zone.Right, zone.Top),
//                             new PointF(zone.Right, zone.Bottom), new PointF(zone.Left, zone.Bottom)
//                         });
//                     }
//                 });
//                 await imgDrawn.SaveAsJpegAsync(drawnPath);
//             }

//             // 2. IMAGEM MASCARADA (O que o C# enviaria para processamento)
//             // Em vez de "recortar" um quadrado, nós apagamos a HUD pintando ela de PRETO.
//             // Assim a IA/OpenCV só vai enxergar o miolo dinâmico do jogo!
//             string maskedPath = Path.Combine(debugDir, $"HUD_MASKED_{Guid.NewGuid().ToString()[..6]}.jpg");
//             using (var imgMasked = await Image.LoadAsync(originalPath))
//             {
//                 imgMasked.Mutate(ctx =>
//                 {
//                     foreach (var zone in hudMask.BlockedZones)
//                     {
//                         ctx.Fill(Color.Black, zone); // Pinta a HUD de preto sólido
//                     }
//                 });
//                 await imgMasked.SaveAsJpegAsync(maskedPath);
//             }

//             sw.Stop();

//             return Ok(new
//             {
//                 message = "Contorno Dinâmico Gerado!",
//                 timeTakenSeconds = sw.Elapsed.TotalSeconds,
//                 zonesCreated = hudMask.BlockedZones.Count,
//                 filesGenerated = new { contourImage = drawnPath, maskedImage = maskedPath }
//             });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, $"Erro interno: {ex.Message}");
//         }
//     }

//     [HttpDelete("clear-hud")]
//     public IActionResult ClearHudFolder()
//     {
//         string debugDir = Path.Combine(_storage.GetBasePath(), "uploads", "debug_hud");
//         if (Directory.Exists(debugDir))
//         {
//             var files = Directory.GetFiles(debugDir);
//             foreach (var file in files) System.IO.File.Delete(file);
//             return Ok($"Pasta limpa! {files.Length} arquivos removidos.");
//         }
//         return Ok("Pasta já estava limpa.");
//     }
// }
using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Requests;
using RoK.Ocr.Api.Dtos.Responses;
using RoK.Ocr.Application.Services;
using RoK.Ocr.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg; // Use JPEG for speed
using System.Diagnostics;

namespace RoK.Ocr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private readonly OcrOrchestrator _orchestrator;
    private readonly IImageStorage _storage;
    private readonly IOcrService _ocrService;

    private const int MAX_WIDTH = 1920; 

    public OcrController(
        OcrOrchestrator orchestrator,
        IImageStorage storage,
        IOcrService ocrService)
    {
        _orchestrator = orchestrator;
        _storage = storage;
        _ocrService = ocrService;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromForm] OcrUploadRequest request)
    {
        var sw = Stopwatch.StartNew();

        if (request.Image == null || request.Image.Length == 0)
            return BadRequest(new OcrApiResponse { Success = false, Message = "No image sent." });

        string physicalPath = "";
        byte[] finalBytes;

        try
        {
            // --- OPTIMIZATION: Conditional Logic ---
            // First, we read only the image header to determine size (without loading pixels)
            // This is ultra-fast.
            using (var stream = request.Image.OpenReadStream())
            {
                var imageInfo = await Image.IdentifyAsync(stream);
                
                // Reset stream to the beginning
                stream.Position = 0;

                if (imageInfo != null && imageInfo.Width > MAX_WIDTH)
                {
                    // CASE 1: Giant Image (4K) -> Worth spending time resizing
                    using var image = await Image.LoadAsync(stream);
                    
                    var newHeight = (int)(image.Height * ((double)MAX_WIDTH / image.Width));
                    image.Mutate(x => x.Resize(MAX_WIDTH, newHeight, KnownResamplers.Bicubic));

                    using var ms = new MemoryStream();
                    // We use JPEG instead of PNG. Much faster to write.
                    await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 });
                    finalBytes = ms.ToArray();
                }
                else
                {
                    // CASE 2: Normal Image (Full HD or smaller) -> DO NOTHING!
                    // Copy original bytes directly. Zero CPU overhead.
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    finalBytes = ms.ToArray();
                }
            }

            // --- PHASE 2: SAVE TO DISK (For the Magnifier to use if needed) ---
            using (var fileStream = new MemoryStream(finalBytes))
            {
                physicalPath = await _storage.SaveImageAsync(fileStream, request.Image.FileName);
            }

            // --- PHASE 3: SEND TO PYTHON ---
            var initialRead = await _ocrService.AnalyzeImageAsync(physicalPath, finalBytes);

            if (initialRead.Blocks == null || !initialRead.Blocks.Any())
            {
                return Ok(new OcrApiResponse
                {
                    Success = false,
                    Message = "Could not detect text in the image.",
                    ProcessingTimeSeconds = sw.Elapsed.TotalSeconds
                });
            }

            // --- PHASE 4: ORCHESTRATION ---
            var profile = await _orchestrator.AnalyzeAsync(
                physicalPath, 
                initialRead.Blocks,
                request.DraftId ?? 0
            );

            sw.Stop();

            return Ok(new OcrApiResponse
            {
                Success = true,
                Message = "OCR completed successfully.",
                RawText = initialRead.FullText,
                Data = profile,
                ProcessingTimeSeconds = sw.Elapsed.TotalSeconds
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL ERROR: {ex}");
            return StatusCode(500, new OcrApiResponse { Success = false, Message = $"Internal error: {ex.Message}" });
        }
    }
}
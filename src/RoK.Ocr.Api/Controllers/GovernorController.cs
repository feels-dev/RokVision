using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Governor;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Application.Features.Governor.Orchestrator;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Diagnostics;

namespace RoK.Ocr.Api.Controllers;

[ApiController]
[Route("api/governor")]
public class GovernorController : ControllerBase
{
    private readonly GovernorOrchestrator _orchestrator;
    private readonly IImageStorage _storage;
    private readonly IOcrService _ocrService;
    private readonly ILogger<GovernorController> _logger;
    private const int MAX_WIDTH = 1920;

    public GovernorController(GovernorOrchestrator orchestrator, IImageStorage storage, IOcrService ocrService, ILogger<GovernorController> logger)
    {
        _orchestrator = orchestrator;
        _storage = storage;
        _ocrService = ocrService;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(RokResponse<GovernorProfile>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] GovernorUploadRequest request)
    {
        // 1. Global Request Start
        var swGlobal = Stopwatch.StartNew();

        if (request.Image == null || request.Image.Length == 0)
            return BadRequest(ResponseFactory.CreateFailure<GovernorProfile>("No image sent.", "ERR_NO_IMAGE", 400));

        string physicalPath = "";
        byte[] finalBytes;
        double resizeScale = 1.0;
        int originalWidth = 0;
        int originalHeight = 0;

        try
        {
            // 2. Image Processing
            using (var stream = request.Image.OpenReadStream())
            {
                var imageInfo = await Image.IdentifyAsync(stream);
                stream.Position = 0;
                originalWidth = imageInfo.Width;
                originalHeight = imageInfo.Height;

                if (imageInfo != null && imageInfo.Width > MAX_WIDTH)
                {
                    using var image = await Image.LoadAsync(stream);
                    resizeScale = (double)MAX_WIDTH / image.Width;
                    var newHeight = (int)(image.Height * resizeScale);
                    image.Mutate(x => x.Resize(MAX_WIDTH, newHeight, KnownResamplers.Bicubic));

                    using var ms = new MemoryStream();
                    await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 });
                    finalBytes = ms.ToArray();
                }
                else
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    finalBytes = ms.ToArray();
                }
            }

            // 3. Save to Disk
            using (var fileStream = new MemoryStream(finalBytes))
            {
                physicalPath = await _storage.SaveImageAsync(fileStream, request.Image.FileName);
            }

            // 4. Initial Read (Python)
            var swPython = Stopwatch.StartNew();
            var initialRead = await _ocrService.AnalyzeImageAsync(physicalPath, finalBytes);
            swPython.Stop();

            if (initialRead.Blocks == null || !initialRead.Blocks.Any())
                return Ok(ResponseFactory.CreateFailure<GovernorProfile>("Could not detect text.", "ERR_OCR_EMPTY", 200));

            // 5. Orchestration
            string correlationId = Guid.NewGuid().ToString();
            var (profile, context) = await _orchestrator.AnalyzeAsync(physicalPath, initialRead.Blocks, originalWidth, originalHeight, request.DraftId ?? 0);

            // =================================================================
            // 6. POPULATING DEBUG (If requested)
            // =================================================================
            if (request.Debug)
            {
                // Fill heavy data only if flag == true
                context.DebugInfo.RawText = initialRead.FullText;
                context.DebugInfo.ImagePath = physicalPath;

                // Add Python time to context timings
                context.DebugInfo.Timings["PythonInitialRead"] = swPython.Elapsed.TotalMilliseconds;
            }

            swGlobal.Stop();
            context.DebugInfo.Timings["TotalGlobalController"] = swGlobal.Elapsed.TotalMilliseconds;

            // Create the standardized Image Context
            var imageContext = new ImageContextDto
            {
                OriginalWidth = originalWidth,
                OriginalHeight = originalHeight,
                ResizedScale = resizeScale
            };

            // Leverage the smart ResponseFactory to build the Enterprise JSON
            var response = ResponseFactory.CreateSuccess(
                businessSummary: profile,
                context: context,
                correlationId: correlationId,
                imageContext: imageContext
            );

            // Attach rich debug object (if Debug=false, properties remain null/empty)
            if (request.Debug)
            {
                response.Debug = context.DebugInfo;
            }
            else
            {
                response.Debug = null;
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical Error in GovernorController");
            return StatusCode(500, ResponseFactory.CreateFailure<GovernorProfile>($"Internal error: {ex.Message}"));
        }
    }
}

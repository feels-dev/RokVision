using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Rally;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Application.Common.Models;
using RoK.Ocr.Application.Features.Rally.Orchestrator;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models.Rally;
using System.Diagnostics;

namespace RoK.Ocr.Api.Controllers;

[ApiController][Route("api/rally")]
public class RallyController : ControllerBase
{
    private readonly RallyOrchestrator _orchestrator;
    private readonly IImageStorage _storage;
    private readonly ILogger<RallyController> _logger;

    public RallyController(
        RallyOrchestrator orchestrator,
        IImageStorage storage,
        ILogger<RallyController> logger)
    {
        _orchestrator = orchestrator;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes screenshots of an Alliance Rally (Header + Participant List).
    /// Supports multiple images to handle scrolling lists.
    /// </summary>
    [HttpPost("analyze")][Consumes("multipart/form-data")][ProducesResponseType(typeof(RokResponse<RallyResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] RallyUploadRequest request)
    {
        var swGlobal = Stopwatch.StartNew();
        var savedPaths = new List<string>();
        string correlationId = Guid.NewGuid().ToString();

        // 1. Validation
        if (request.Images == null || request.Images.Count == 0)
        {
            return BadRequest(ResponseFactory.CreateFailure<RallyResult>(
                "No images provided. Please upload at least one screenshot.", 
                "ERR_NO_IMAGE", 
                400,
                correlationId));
        }

        try
        {
            // 2. Save all images to disk
            foreach (var file in request.Images)
            {
                if (file.Length > 0)
                {
                    using var stream = file.OpenReadStream();
                    // Generate a unique path for each scroll part
                    string path = await _storage.SaveImageAsync(stream, file.FileName);
                    savedPaths.Add(path);
                }
            }

            _logger.LogInformation("Starting Rally Analysis with {Count} images. TraceID: {TraceId}", savedPaths.Count, correlationId);

            // 3. Call Orchestrator (The heavy lifting)
            var (result, context) = await _orchestrator.AnalyzeAsync(savedPaths);

            swGlobal.Stop();

            // 4. Build Enterprise Response
            var imageContext = new ImageContextDto
            {
                OriginalWidth = context.ImageWidth,
                OriginalHeight = context.ImageHeight,
                ResizedScale = 1.0 // Currently Rally does not resize in the controller
            };

            var response = ResponseFactory.CreateSuccess(
                businessSummary: result,
                context: context,
                correlationId: correlationId,
                imageContext: imageContext
            );

            // 5. Populate Debug Info
            if (request.Debug)
            {
                response.Debug = context.DebugInfo;
                // Add specific metadata about the scroll stitch
                context.DebugInfo.Timings["TotalImages"] = savedPaths.Count;
                context.DebugInfo.Timings["TotalGlobalController"] = swGlobal.Elapsed.TotalMilliseconds;
            }
            else
            {
                response.Debug = null;
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical Error in RallyController");
            return StatusCode(500, ResponseFactory.CreateFailure<RallyResult>($"Internal error: {ex.Message}", "ERR_INTERNAL", 500, correlationId));
        }
        finally
        {
            // 6. Cleanup: Delete all temporary images
            foreach (var path in savedPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    try { System.IO.File.Delete(path); } catch { /* Ignore delete errors */ }
                }
            }
        }
    }
}
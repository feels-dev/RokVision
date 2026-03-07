using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Map;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Application.Features.Map.Orchestrator;
using RoK.Ocr.Domain.Models.Map;
using System.Diagnostics;

namespace RoK.Ocr.Api.Controllers;

[ApiController]
[Route("api/map")]
public class MapController : ControllerBase
{
    private readonly MapOrchestrator _orchestrator;
    private readonly ILogger<MapController> _logger;

    public MapController(MapOrchestrator orchestrator, ILogger<MapController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(RokResponse<MapAnalysisResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] MapUploadRequest request)
    {
        var swGlobal = Stopwatch.StartNew();
        string correlationId = Guid.NewGuid().ToString();

        if (request.Image == null || request.Image.Length == 0)
            return BadRequest(ResponseFactory.CreateFailure<MapAnalysisResult>("No image sent.", "ERR_NO_IMAGE", 400, correlationId));

        try
        {
            var (result, context) = await _orchestrator.AnalyzeAsync(request.Image.OpenReadStream(), request.Image.FileName);

            swGlobal.Stop();

            double overallConfidence = 90.0; 
            if (context.ExtractedFields.Any())
            {
                overallConfidence = context.ExtractedFields.Values.Average(e => e.Confidence);
            }

            var imageContext = new ImageContextDto
            {
                OriginalWidth = context.ImageWidth > 0 ? context.ImageWidth : 1920,
                OriginalHeight = context.ImageHeight > 0 ? context.ImageHeight : 1080,
                ResizedScale = 1.0
            };

            var response = ResponseFactory.CreateSuccess(
                businessSummary: result,
                context: context,
                correlationId: correlationId,
                imageContext: imageContext
            );

            if (request.Debug)
            {
                response.Debug = context.DebugInfo;
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
            _logger.LogError(ex, "Critical Error in MapController");
            return StatusCode(500, ResponseFactory.CreateFailure<MapAnalysisResult>($"Internal error: {ex.Message}", "ERR_INTERNAL", 500, correlationId));
        }
    }
}
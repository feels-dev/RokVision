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
        // 1. Start Global Timer
        var swGlobal = Stopwatch.StartNew();

        if (request.Image == null || request.Image.Length == 0)
            return BadRequest(ResponseFactory.CreateFailure<MapAnalysisResult>("No image sent.", "ERR_NO_IMAGE", 400));

        try
        {
            // 2. Orchestration
            // The orchestrator handles file saving and logic
            var (result, context) = await _orchestrator.AnalyzeAsync(request.Image.OpenReadStream(), request.Image.FileName);

            swGlobal.Stop();

            // 3. Construct Response
            // Calculate overall confidence based on evidence found (coordinates + batch confidence average)
            double overallConfidence = 90.0; // Default high for map logic
            if (context.Evidence.Any())
            {
                overallConfidence = context.Evidence.Values.Average(e => e.Confidence);
            }

            var response = ResponseFactory.CreateSuccess(
                summary: result,
                fields: context.Evidence,
                auditLog: context.AuditLog,
                processingTime: swGlobal.Elapsed.TotalSeconds,
                overallConfidence: overallConfidence, 
                warnings: context.Warnings
            );

            // 4. Attach Debug Info if requested
            if (request.Debug)
            {
                response.Debug = context.DebugInfo;
            }
            else
            {
                // Ensure clear JSON output for production
                response.Debug = null;
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical Error in MapController");
            return StatusCode(500, ResponseFactory.CreateFailure<MapAnalysisResult>($"Internal error: {ex.Message}"));
        }
    }
}
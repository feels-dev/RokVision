using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Experience;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Application.Features.Experience.Orchestrator;
using RoK.Ocr.Domain.Models.Experience;
using System.Diagnostics;
using System.Linq;

namespace RoK.Ocr.Api.Controllers;

[ApiController]
[Route("api/xp")]
public class ExperienceController : ControllerBase
{
    private readonly XpOrchestrator _orchestrator;
    private readonly ILogger<ExperienceController> _logger;

    public ExperienceController(XpOrchestrator orchestrator, ILogger<ExperienceController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RokResponse<XpInventoryData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] XpUploadRequest request)
    {
        var swGlobal = Stopwatch.StartNew();
        string correlationId = Guid.NewGuid().ToString();

        if (request.Images == null || !request.Images.Any())
            return BadRequest(ResponseFactory.CreateFailure<XpInventoryData>("No images provided.", "ERR_NO_IMAGES", 400, correlationId));

        try
        {
            // Passing debug flag and correlationId to the orchestrator
            var (data, context) = await _orchestrator.ProcessXpAsync(request.Images, request.Debug);
            swGlobal.Stop();

            // Build Enterprise Response
            // Note: Since we process multiple images, we use the first one as the reference context dimensions
            var imageContext = new ImageContextDto
            {
                OriginalWidth = context.ImageWidth > 0 ? context.ImageWidth : 1920,
                OriginalHeight = context.ImageHeight > 0 ? context.ImageHeight : 1080,
                ResizedScale = 1.0
            };

            var response = ResponseFactory.CreateSuccess(
                businessSummary: data,
                context: context,
                correlationId: correlationId,
                imageContext: imageContext
            );

            // Attaching Debug if flag is active
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
            _logger.LogError(ex, "XP Endpoint Critical Error");
            return StatusCode(500, ResponseFactory.CreateFailure<XpInventoryData>($"Internal Server Error: {ex.Message}", "ERR_INTERNAL", 500, correlationId));
        }
    }
}
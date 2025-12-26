using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Experience;
using RoK.Ocr.Application.Features.Experience.Orchestrator;
using System.Diagnostics;

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
    [ProducesResponseType(typeof(XpApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] XpUploadRequest request)
    {
        var sw = Stopwatch.StartNew();

        if (request.Images == null || !request.Images.Any())
            return BadRequest(new XpApiResponse { Success = false, Message = "No images provided." });

        try
        {
            var (data, rawText) = await _orchestrator.ProcessXpAsync(request.Images);
            sw.Stop();

            return Ok(new XpApiResponse
            {
                Success = true,
                Message = data.Items.Any() 
                    ? $"Success. {data.Items.Count} XP book types found." 
                    : "Analysis complete. No XP books found.",
                ProcessingTimeSeconds = Math.Round(sw.Elapsed.TotalSeconds, 3),
                RawText = rawText,
                Data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XP Endpoint Error");
            return StatusCode(500, new XpApiResponse { Success = false, Message = ex.Message });
        }
    }
}
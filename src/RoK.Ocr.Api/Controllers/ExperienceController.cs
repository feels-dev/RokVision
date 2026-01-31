using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Experience;
using RoK.Ocr.Application.Features.Experience.Orchestrator;
using RoK.Ocr.Domain.Models.Experience;
using System.Diagnostics;
using RoK.Ocr.Application.Common.Dtos; 
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

        if (request.Images == null || !request.Images.Any())
            return BadRequest(ResponseFactory.CreateFailure<XpInventoryData>("No images provided.", "ERR_NO_IMAGES", 400)); 

        try
        {
            // Passing debug flag
            var (data, context) = await _orchestrator.ProcessXpAsync(request.Images, request.Debug);
            swGlobal.Stop();

            // OVERALL CONFIDENCE CALCULATION
            double overallConf = data.Items.Any() 
                ? data.Items.Average(i => i.Confidence) 
                : 0.0;

            var response = ResponseFactory.CreateSuccess(
                summary: data,
                fields: context.Evidence,
                auditLog: context.AuditLog,
                processingTime: swGlobal.Elapsed.TotalSeconds,
                overallConfidence: overallConf,
                warnings: context.Warnings
            );

            // Attaching Debug if flag is active
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
            _logger.LogError(ex, "XP Endpoint Error");
            return StatusCode(500, ResponseFactory.CreateFailure<XpInventoryData>($"Internal Server Error: {ex.Message}")); 
        }
    }
}
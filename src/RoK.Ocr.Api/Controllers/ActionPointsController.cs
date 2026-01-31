using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.ActionPoints;
using RoK.Ocr.Application.Common.Dtos;
using RoK.Ocr.Application.Features.ActionPoints.Orchestrator;
using RoK.Ocr.Domain.Models.ActionPoints;
using System.Diagnostics;
using System.Linq;

namespace RoK.Ocr.Api.Controllers;

[ApiController]
[Route("api/ap")]
public class ActionPointsController : ControllerBase
{
    private readonly ApOrchestrator _orchestrator;
    private readonly ILogger<ActionPointsController> _logger;

    public ActionPointsController(ApOrchestrator orchestrator, ILogger<ActionPointsController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RokResponse<ApInventoryData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] ApUploadRequest request)
    {
        var swGlobal = Stopwatch.StartNew();

        if (request.Images == null || !request.Images.Any())
        {
            return BadRequest(ResponseFactory.CreateFailure<ApInventoryData>("No images provided in the request.", "ERR_NO_IMAGES", 400));
        }

        try
        {
            _logger.LogInformation("Receiving {Count} AP inventory images for analysis.", request.Images.Count);

            // Passing the debug flag
            var (inventoryData, context) = await _orchestrator.ProcessInventoryAsync(request.Images, request.Debug);

            swGlobal.Stop();

            // Overall Confidence Calculation (Uses what was logged in Context)
            double overallConf = inventoryData.Items.Any()
                ? inventoryData.Items.Average(i => i.Confidence)
                : 0.0;

            var response = ResponseFactory.CreateSuccess(
                summary: inventoryData,
                fields: context.Evidence,
                auditLog: context.AuditLog,
                processingTime: swGlobal.Elapsed.TotalSeconds,
                overallConfidence: overallConf,
                warnings: context.Warnings
            );

            // Attaching Debug info if flag is active
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
            _logger.LogError(ex, "Critical error in Action Points endpoint.");
            return StatusCode(500, ResponseFactory.CreateFailure<ApInventoryData>($"Internal Server Error: {ex.Message}"));
        }
    }
}
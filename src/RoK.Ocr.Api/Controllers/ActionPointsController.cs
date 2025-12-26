using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.ActionPoints;
using RoK.Ocr.Application.Features.ActionPoints.Orchestrator;
using System.Diagnostics;

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
    [ProducesResponseType(typeof(ApApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] ApUploadRequest request)
    {
        var sw = Stopwatch.StartNew();

        if (request.Images == null || !request.Images.Any())
        {
            return BadRequest(new ApApiResponse 
            { 
                Success = false, 
                Message = "No images provided in the request." 
            });
        }

        try
        {
            _logger.LogInformation("Receiving {Count} AP inventory images for analysis.", request.Images.Count);

            var (inventoryData, rawText) = await _orchestrator.ProcessInventoryAsync(request.Images);

            sw.Stop();

            var response = new ApApiResponse
            {
                Success = true,
                Message = inventoryData.Items.Any() 
                    ? $"Analysis complete. {inventoryData.Items.Count} item types identified." 
                    : "Analysis complete, but no standard items were identified.",
                
                ProcessingTimeSeconds = Math.Round(sw.Elapsed.TotalSeconds, 3),
                RawText = rawText, 
                Data = inventoryData
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in Action Points endpoint.");
            return StatusCode(500, new ApApiResponse 
            { 
                Success = false, 
                Message = $"Internal Server Error: {ex.Message}",
                RawText = "Log unavailable due to critical failure."
            });
        }
    }
}
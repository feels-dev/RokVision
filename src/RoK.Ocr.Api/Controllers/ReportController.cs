using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Reports;
using RoK.Ocr.Application.Common.Dtos; // RokResponse, Factory
using RoK.Ocr.Application.Features.Reports.Orchestrator;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models.Reports;
using System.Diagnostics;

namespace RoK.Ocr.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly ReportOrchestrator _orchestrator;
    private readonly IImageStorage _storage;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        ReportOrchestrator orchestrator,
        IImageStorage storage,
        ILogger<ReportController> logger)
    {
        _orchestrator = orchestrator;
        _storage = storage;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RokResponse<ReportResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] ReportUploadRequest request)
    {
        var swGlobal = Stopwatch.StartNew();

        if (request.Image == null || request.Image.Length == 0)
            return BadRequest(ResponseFactory.CreateFailure<ReportResult>("No image selected.", "ERR_NO_IMAGE", 400));

        string physicalPath = string.Empty;

        try
        {
            // 1. Save Image
            using (var stream = request.Image.OpenReadStream())
            {
                physicalPath = await _storage.SaveImageAsync(stream, request.Image.FileName);
            }

            // 2. Call Orchestrator
            // The orchestrator manages its own timers internally
            var (data, context) = await _orchestrator.AnalyzeAsync(physicalPath, request.Debug);

            swGlobal.Stop();

            // 3. Build Rich Response
            var response = ResponseFactory.CreateSuccess(
                summary: data,
                fields: context.Evidence,
                auditLog: context.AuditLog,
                processingTime: swGlobal.Elapsed.TotalSeconds,
                overallConfidence: data.OverallConfidence,
                warnings: context.Warnings
            );

            // 4. Populate Debug if requested
            if (request.Debug)
            {
                // Basic image info (For reports, Python returns the processed canvas size, 
                // which the orchestrator puts in the context)
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
            _logger.LogError(ex, "[ReportController] CRITICAL ERROR: {Message}", ex.Message);
            return StatusCode(500, ResponseFactory.CreateFailure<ReportResult>($"Internal server error: {ex.Message}"));
        }
    }
}
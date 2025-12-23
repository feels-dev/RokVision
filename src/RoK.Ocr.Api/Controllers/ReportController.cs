using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Api.Dtos.Reports;
using RoK.Ocr.Application.Features.Reports.Orchestrator; // Namespace updated expectation
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
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger;
    }

    /// <summary>
    /// Receives a battle report screenshot, isolates the beige paper container, 
    /// identifies the combat type (PvP/PvE), and extracts all combat metrics, names, and commanders.
    /// </summary>
    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ReportApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] ReportUploadRequest request)
    {
        var sw = Stopwatch.StartNew();

        if (request.Image == null || request.Image.Length == 0)
        {
            return BadRequest(new ReportApiResponse { Success = false, Message = "Error: No image selected." });
        }

        string physicalPath = string.Empty;

        try
        {
            using (var stream = request.Image.OpenReadStream())
            {
                physicalPath = await _storage.SaveImageAsync(stream, request.Image.FileName);
            }

            // --- ATTENTION HERE: Receiving the Tuple ---
            var (data, rawText) = await _orchestrator.AnalyzeAsync(physicalPath);

            sw.Stop();

            return Ok(new ReportApiResponse
            {
                Success = true,
                Message = "Report processed successfully.",
                ProcessingTimeSeconds = Math.Round(sw.Elapsed.TotalSeconds, 3),

                // Now we take the rawText that came from the tuple, not from the 'data' object
                RawText = rawText,

                // The 'data' object (ReportResult) is now clean and without duplicate RawText
                Data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReportController] CRITICAL ERROR: {Message}", ex.Message);
            return StatusCode(500, new ReportApiResponse { Success = false, Message = "Internal server error." });
        }
    }
}
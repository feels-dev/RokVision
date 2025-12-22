using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoK.Ocr.Application.Reports.Services;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Domain.Models.Reports;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace RoK.Ocr.Api.Controllers;

/// <summary>
/// DTO (Data Transfer Object) to represent the report submission form.
/// Used by Swagger/OpenAPI to render the file upload field correctly.
/// </summary>
public class ReportUploadDto
{
    [Required]
    public IFormFile Image { get; set; } = null!;
}

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
    /// <param name="request">The form-data request containing the image file.</param>
    /// <returns>A structured JSON containing extracted data and a dynamic confidence score.</returns>
    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ReportResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analyze([FromForm] ReportUploadDto request)
    {
        var sw = Stopwatch.StartNew();

        if (request.Image == null || request.Image.Length == 0)
        {
            return BadRequest(new { success = false, message = "Error: No image selected." });
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

            return Ok(new
            {
                success = true,
                message = "Report processed successfully.",
                processingTimeSeconds = Math.Round(sw.Elapsed.TotalSeconds, 3),

                // Now we take the rawText that came from the tuple, not from the 'data' object
                rawText = rawText,

                // The 'data' object (ReportResult) is now clean and without duplicate RawText
                data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReportController] CRITICAL ERROR: {Message}", ex.Message);
            return StatusCode(500, new { success = false, message = "Internal server error." });
        }
    }
}
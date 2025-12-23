using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RoK.Ocr.Api.Dtos.Reports;

/// <summary>
/// DTO to represent the report submission form.
/// </summary>
public class ReportUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;
}
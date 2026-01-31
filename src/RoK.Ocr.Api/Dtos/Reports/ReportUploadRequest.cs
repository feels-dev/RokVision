using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RoK.Ocr.Api.Dtos.Reports;

public class ReportUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;
    public bool Debug { get; set; } = false;
}
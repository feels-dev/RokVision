using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RoK.Ocr.Api.Dtos.Map;

public class MapUploadRequest
{
    /// <summary>
    /// The screenshot of the Kingdom Map.
    /// </summary>
    [Required]
    public IFormFile Image { get; set; } = null!;

    /// <summary>
    /// If true, returns detailed debug info (ROI, timings).
    /// </summary>
    public bool Debug { get; set; } = false;
}
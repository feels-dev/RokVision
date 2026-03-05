using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RoK.Ocr.Api.Dtos.Rally;

public class RallyUploadRequest
{
    /// <summary>
    /// List of screenshots containing the Rally information.
    /// Accepts multiple images to support scrolling through the participant list.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one image is required.")]
    public List<IFormFile> Images { get; set; } = new();

    /// <summary>
    /// If true, returns detailed debug information including 
    /// OCR blocks, timings, and raw extraction data.
    /// </summary>
    public bool Debug { get; set; } = false;
}
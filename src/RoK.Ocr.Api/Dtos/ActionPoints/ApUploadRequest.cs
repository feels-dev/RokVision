using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RoK.Ocr.Api.Dtos.ActionPoints;

public class ApUploadRequest
{
    [Required]
    public List<IFormFile> Images { get; set; } = new();
}
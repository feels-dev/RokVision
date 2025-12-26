using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RoK.Ocr.Api.Dtos.Experience;

public class XpUploadRequest
{
    [Required]
    public List<IFormFile> Images { get; set; } = new();
}
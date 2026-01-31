using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RoK.Ocr.Api.Dtos.Governor;

public class GovernorUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;
    public int? DraftId { get; set; }
    public bool Debug { get; set; } = false;
}
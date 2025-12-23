using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RoK.Ocr.Api.Dtos.Governor;

public class GovernorUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;
    
    // Optional: Draft ID in case OCR fails to read the ID
    public int? DraftId { get; set; }
}
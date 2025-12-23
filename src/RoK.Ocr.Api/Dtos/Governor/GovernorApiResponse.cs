using RoK.Ocr.Domain.Models; // Assumes GovernorProfile is here

namespace RoK.Ocr.Api.Dtos.Governor;

public class GovernorApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    // All the text found by the OCR in the image
    public string RawText { get; set; } = string.Empty;

    public GovernorProfile? Data { get; set; }
    public double ProcessingTimeSeconds { get; set; }
}
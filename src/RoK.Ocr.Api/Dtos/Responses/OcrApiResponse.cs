using RoK.Ocr.Domain.Models;

namespace RoK.Ocr.Api.Dtos.Responses;

public class OcrApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    // NEW FIELD: All the text found by the OCR in the image
    public string RawText { get; set; } = string.Empty;

    public GovernorProfile? Data { get; set; }
    public double ProcessingTimeSeconds { get; set; }
}
using RoK.Ocr.Domain.Models.Experience;

namespace RoK.Ocr.Api.Dtos.Experience;

public class XpApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double ProcessingTimeSeconds { get; set; }
    public string RawText { get; set; } = string.Empty;
    public XpInventoryData? Data { get; set; }
}
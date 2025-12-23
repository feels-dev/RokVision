using RoK.Ocr.Domain.Models.Reports; // Assumes ReportResult is here

namespace RoK.Ocr.Api.Dtos.Reports;

public class ReportApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double ProcessingTimeSeconds { get; set; }
    public string RawText { get; set; } = string.Empty;
    public ReportResult? Data { get; set; }
}
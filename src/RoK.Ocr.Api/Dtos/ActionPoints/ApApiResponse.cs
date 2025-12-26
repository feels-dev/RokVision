using RoK.Ocr.Domain.Models.ActionPoints;

namespace RoK.Ocr.Api.Dtos.ActionPoints;

public class ApApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double ProcessingTimeSeconds { get; set; }

    // NOVO CAMPO: Essencial para debug
    public string RawText { get; set; } = string.Empty;

    public ApInventoryData? Data { get; set; }
}
using RoK.Ocr.Domain.Enums;

namespace RoK.Ocr.Domain.Models;

public class AnalyzedBlock
{
    public required OcrBlock Raw { get; set; } 
    
    public BlockType Type { get; set; } = BlockType.Unknown;
    public double CanvasWidth { get; set; }
    public double CanvasHeight { get; set; }
    public (double X, double Y) Center => Raw.Center;

    public (double X, double Y) NormalizedCenter => (
        CanvasWidth > 0 ? Raw.Center.X / CanvasWidth : Raw.Center.X,
        CanvasHeight > 0 ? Raw.Center.Y / CanvasHeight : Raw.Center.Y
    );
}
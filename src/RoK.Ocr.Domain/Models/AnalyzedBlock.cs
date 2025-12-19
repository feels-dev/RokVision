using RoK.Ocr.Domain.Enums;

namespace RoK.Ocr.Domain.Models;

public class AnalyzedBlock
{
    public OcrBlock Raw { get; set; } // The original OCR block (Text + Box)
    public BlockType Type { get; set; } = BlockType.Unknown;
    
    // Shortcut to facilitate distance calculations
    public (double X, double Y) Center => Raw.Center;
}
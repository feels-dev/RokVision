using System;

namespace RoK.Ocr.Application.Common.Models;

/// <summary>
/// Represents a specific region of interest in the image to be processed by OCR.
/// Can originate from YOLO detection, Shield Fallback, or Text Heuristic.
/// </summary>
public class OcrRegionCandidate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    // [x, y, w, h]
    public int[] Box { get; set; } = new int[4];
    
    public string Strategy { get; set; } = "Sharpen";
    
    public bool HasShield { get; set; }
    
    public double CenterX { get; set; }
    
    public double CenterY { get; set; }
    
    // "YOLO_Label", "Fallback_Shield", "Text_Fallback"
    public string Source { get; set; } = string.Empty; 
}
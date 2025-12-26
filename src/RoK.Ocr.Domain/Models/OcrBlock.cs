using System.Collections.Generic;

namespace RoK.Ocr.Domain.Models;

public class OcrBlock
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string DominantColor { get; set; } = "Unknown";

    // Coordinates: [[x1,y1], [x2,y2], [x3,y3], [x4,y4]]
    public List<List<double>> Box { get; set; } = new();
    public string CustomId { get; set; } = string.Empty;

    // Helper to calculate the block center (essential for calculating distances)
    public (double X, double Y) Center
    {
        get
        {
            if (Box == null || Box.Count < 3) return (0, 0);
            // Average between top-left and bottom-right
            var centerX = (Box[0][0] + Box[2][0]) / 2;
            var centerY = (Box[0][1] + Box[2][1]) / 2;
            return (centerX, centerY);
        }
    }
}
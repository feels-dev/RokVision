namespace RoK.Ocr.Domain.Models;

public class ExtractionResult<T>
{
    public T Value { get; set; }
    public double Confidence { get; set; } // 0 to 100
    public AnalyzedBlock? SourceBlock { get; set; } // Which block originated this data?
    
    // If confidence is high, it is considered a success
    public bool IsSuccess => Confidence > 60; 
}
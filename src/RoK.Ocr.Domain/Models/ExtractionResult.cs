namespace RoK.Ocr.Domain.Models;

public class ExtractionResult<T>
{
    public T Value { get; set; } = default!; 
    
    public double Confidence { get; set; }
    public AnalyzedBlock? SourceBlock { get; set; }

    public bool IsSuccess => Confidence > 60;
}
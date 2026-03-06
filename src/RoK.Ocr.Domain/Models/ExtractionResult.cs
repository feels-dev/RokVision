using System;

namespace RoK.Ocr.Domain.Models;

/// <summary>
/// Container for the result of a neural or heuristic extraction attempt.
/// </summary>
/// <typeparam name="T">The type of the extracted value.</typeparam>
public class ExtractionResult<T>
{
    /// <summary>
    /// The sanitized and parsed value ready for business use.
    /// </summary>
    public T Value { get; set; } = default!;

    /// <summary>
    /// Confidence score of the extraction (0.0 to 100.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The exact internal branch or logic strategy used to find this result (e.g., "IdNeuron_StrictLabel").
    /// </summary>
    public string Strategy { get; set; } = "DefaultStrategy";

    /// <summary>
    /// The original OCR block that served as the source for this extraction.
    /// Contains the raw text and spatial bounding box.
    /// </summary>
    public AnalyzedBlock? SourceBlock { get; set; }

    /// <summary>
    /// Indicates if the extraction was successful based on the confidence threshold.
    /// </summary>
    public bool IsSuccess => Confidence > 0;
}
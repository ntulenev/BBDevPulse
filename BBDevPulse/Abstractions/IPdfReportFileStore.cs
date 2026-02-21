using QuestPDF.Infrastructure;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Persists generated PDF report document.
/// </summary>
public interface IPdfReportFileStore
{
    /// <summary>
    /// Saves generated PDF document to output path.
    /// </summary>
    /// <param name="outputPath">Resolved output path.</param>
    /// <param name="document">QuestPDF document.</param>
    void Save(string outputPath, IDocument document);
}

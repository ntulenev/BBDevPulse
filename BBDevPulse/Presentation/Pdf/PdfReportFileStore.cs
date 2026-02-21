using BBDevPulse.Abstractions;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BBDevPulse.Presentation.Pdf;

/// <summary>
/// Filesystem-backed PDF report store.
/// </summary>
public sealed class PdfReportFileStore : IPdfReportFileStore
{
    /// <inheritdoc />
    public void Save(string outputPath, IDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(document);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            _ = Directory.CreateDirectory(outputDirectory);
        }

        var pdfContent = document.GeneratePdf();
        File.WriteAllBytes(outputPath, pdfContent);
    }
}

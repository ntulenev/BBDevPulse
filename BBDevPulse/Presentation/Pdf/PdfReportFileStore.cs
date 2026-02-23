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
    public Task SaveAsync(string outputPath, IDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(document);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            _ = Directory.CreateDirectory(outputDirectory);
        }

        var pdfContent = document.GeneratePdf();
        return File.WriteAllBytesAsync(outputPath, pdfContent, cancellationToken);
    }
}

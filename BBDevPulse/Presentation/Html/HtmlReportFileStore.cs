using System.Text;

using BBDevPulse.Abstractions;

namespace BBDevPulse.Presentation.Html;

/// <summary>
/// Filesystem-backed HTML report store.
/// </summary>
public sealed class HtmlReportFileStore : IHtmlReportFileStore
{
    /// <inheritdoc />
    public Task SaveAsync(string outputPath, string html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(html);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            _ = Directory.CreateDirectory(outputDirectory);
        }

        return File.WriteAllTextAsync(outputPath, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }
}

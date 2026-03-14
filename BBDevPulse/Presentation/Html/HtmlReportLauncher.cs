using System.Diagnostics;

using BBDevPulse.Abstractions;

namespace BBDevPulse.Presentation.Html;

/// <summary>
/// Opens generated HTML reports in the default browser.
/// </summary>
public sealed class HtmlReportLauncher : IHtmlReportLauncher
{
    /// <inheritdoc />
    public void Open(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = outputPath,
            UseShellExecute = true
        };

        _ = Process.Start(startInfo);
    }
}

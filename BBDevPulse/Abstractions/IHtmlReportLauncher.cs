namespace BBDevPulse.Abstractions;

/// <summary>
/// Opens generated HTML reports in the default browser.
/// </summary>
public interface IHtmlReportLauncher
{
    /// <summary>
    /// Opens the provided HTML file.
    /// </summary>
    /// <param name="outputPath">Path to generated HTML report.</param>
    void Open(string outputPath);
}

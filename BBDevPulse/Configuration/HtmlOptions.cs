namespace BBDevPulse.Configuration;

/// <summary>
/// Raw HTML report options bound from configuration.
/// </summary>
public sealed class HtmlOptions
{
    private const string DEFAULT_OUTPUT_PATH = "bbdevpulse-report.html";

    /// <summary>
    /// Gets or sets whether HTML report generation is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets output file path for generated HTML report.
    /// </summary>
    public string OutputPath { get; init; } = DEFAULT_OUTPUT_PATH;

    /// <summary>
    /// Gets or sets whether generated HTML report should be opened in the default browser.
    /// </summary>
    public bool OpenInBrowser { get; init; }

    /// <summary>
    /// Resolves output path with optional team prefix and date suffix.
    /// </summary>
    /// <param name="teamFilter">Selected team, if any.</param>
    /// <returns>Absolute dated output path.</returns>
    public string ResolveOutputPath(string? teamFilter = null)
    {
        return ReportOutputPath.Resolve(OutputPath, DEFAULT_OUTPUT_PATH, ".html", teamFilter);
    }
}

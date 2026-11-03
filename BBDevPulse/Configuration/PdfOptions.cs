namespace BBDevPulse.Configuration;

/// <summary>
/// Raw PDF report options bound from configuration.
/// </summary>
public sealed class PdfOptions
{
    private const string DEFAULT_OUTPUT_PATH = "bbdevpulse-report.pdf";

    /// <summary>
    /// Gets or sets whether PDF report generation is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets output file path for generated PDF report.
    /// </summary>
    public string OutputPath { get; init; } = DEFAULT_OUTPUT_PATH;

    /// <summary>
    /// Resolves output path with optional team prefix and date suffix.
    /// </summary>
    /// <param name="teamFilter">Selected team, if any.</param>
    /// <returns>Absolute dated output path.</returns>
    public string ResolveOutputPath(string? teamFilter = null)
    {
        return ReportOutputPath.Resolve(OutputPath, DEFAULT_OUTPUT_PATH, ".pdf", teamFilter);
    }
}

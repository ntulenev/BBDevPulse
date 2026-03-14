using System.Globalization;

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
    /// Resolves output path to absolute path and appends date suffix.
    /// </summary>
    /// <returns>Absolute dated output path.</returns>
    public string ResolveOutputPath()
    {
        var candidatePath = string.IsNullOrWhiteSpace(OutputPath)
            ? DEFAULT_OUTPUT_PATH
            : OutputPath.Trim();

        var absolutePath = Path.IsPathRooted(candidatePath)
            ? Path.GetFullPath(candidatePath)
            : Path.GetFullPath(candidatePath, Directory.GetCurrentDirectory());

        return AppendDateSuffix(absolutePath, DateTime.Now);
    }

    private static string AppendDateSuffix(string absolutePath, DateTime currentDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var directoryPath = Path.GetDirectoryName(absolutePath);
        var extension = Path.GetExtension(absolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".html";
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absolutePath);
        var dateSuffix = currentDate.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var datedFileName = fileNameWithoutExtension + "_" + dateSuffix + extension;

        return string.IsNullOrWhiteSpace(directoryPath)
            ? datedFileName
            : Path.Combine(directoryPath, datedFileName);
    }
}

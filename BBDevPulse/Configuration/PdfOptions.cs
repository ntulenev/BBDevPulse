using System.Globalization;

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
            extension = ".pdf";
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absolutePath);
        var dateSuffix = currentDate.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var datedFileName = fileNameWithoutExtension + "_" + dateSuffix + extension;

        return string.IsNullOrWhiteSpace(directoryPath)
            ? datedFileName
            : Path.Combine(directoryPath, datedFileName);
    }
}

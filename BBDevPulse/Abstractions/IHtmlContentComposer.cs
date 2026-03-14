using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Composes standalone HTML for the BBDevPulse report.
/// </summary>
public interface IHtmlContentComposer
{
    /// <summary>
    /// Composes report HTML.
    /// </summary>
    /// <param name="reportData">Aggregated report data.</param>
    /// <returns>Standalone HTML document.</returns>
    string Compose(ReportData reportData);
}

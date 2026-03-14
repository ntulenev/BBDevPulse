using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Renders BBDevPulse HTML report output.
/// </summary>
public interface IHtmlReportRenderer
{
    /// <summary>
    /// Renders and saves HTML report.
    /// </summary>
    /// <param name="reportData">Aggregated report data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RenderReportAsync(ReportData reportData, CancellationToken cancellationToken = default);
}

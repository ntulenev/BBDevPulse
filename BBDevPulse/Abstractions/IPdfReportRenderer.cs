using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Renders BBDevPulse PDF report output.
/// </summary>
public interface IPdfReportRenderer
{
    /// <summary>
    /// Renders and saves PDF report.
    /// </summary>
    /// <param name="reportData">Aggregated report data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RenderReportAsync(ReportData reportData, CancellationToken cancellationToken = default);
}

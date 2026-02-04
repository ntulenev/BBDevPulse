using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Presents statistics reports.
/// </summary>
public interface IStatisticsPresenter
{
    /// <summary>
    /// Renders merge time statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderMergeTimeStats(ReportData reportData);

    /// <summary>
    /// Renders TTFR statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderTtfrStats(ReportData reportData);

    /// <summary>
    /// Renders per-developer statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    /// <param name="filterDate">Filter cutoff date.</param>
    void RenderDeveloperStatsTable(
        ReportData reportData,
        DateTimeOffset filterDate);
}

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
    /// <param name="reports">Pull request reports.</param>
    void RenderMergeTimeStats(IReadOnlyCollection<PullRequestReport> reports);

    /// <summary>
    /// Renders TTFR statistics.
    /// </summary>
    /// <param name="reports">Pull request reports.</param>
    void RenderTtfrStats(IReadOnlyCollection<PullRequestReport> reports);

    /// <summary>
    /// Renders per-developer statistics.
    /// </summary>
    /// <param name="stats">Developer statistics keyed by identity.</param>
    /// <param name="filterDate">Filter cutoff date.</param>
    void RenderDeveloperStatsTable(
        IReadOnlyDictionary<DeveloperKey, DeveloperStats> stats,
        DateTimeOffset filterDate);
}

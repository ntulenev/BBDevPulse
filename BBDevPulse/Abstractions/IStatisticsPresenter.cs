using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Presents statistics reports.
/// </summary>
public interface IStatisticsPresenter
{
    /// <summary>
    /// Renders pull request throughput statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderPrThroughputStats(ReportData reportData);

    /// <summary>
    /// Renders pull request distribution per developer.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderPrsPerDeveloperStats(ReportData reportData);

    /// <summary>
    /// Renders comments statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderCommentsStats(ReportData reportData);

    /// <summary>
    /// Renders peer comments statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderPeerCommentsStats(ReportData reportData);

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
    /// Renders corrections statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderCorrectionsStats(ReportData reportData);

    /// <summary>
    /// Renders pull request size statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderPullRequestSizeStats(ReportData reportData);

    /// <summary>
    /// Renders worst pull requests by metric.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderWorstPullRequestsTable(ReportData reportData);

    /// <summary>
    /// Renders per-developer statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderDeveloperStatsTable(ReportData reportData);

    /// <summary>
    /// Renders detailed per-developer activity sections.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderDeveloperDetails(ReportData reportData);

    /// <summary>
    /// Renders Bitbucket telemetry summary.
    /// </summary>
    /// <param name="telemetrySnapshot">Telemetry snapshot.</param>
    void RenderBitbucketTelemetry(BitbucketTelemetrySnapshot telemetrySnapshot);
}

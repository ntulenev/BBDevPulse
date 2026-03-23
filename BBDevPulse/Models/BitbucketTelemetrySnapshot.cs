namespace BBDevPulse.Models;

/// <summary>
/// Bitbucket telemetry snapshot for the current run.
/// </summary>
/// <param name="IsEnabled">Whether telemetry is enabled.</param>
/// <param name="TotalRequests">Total number of tracked Bitbucket API requests.</param>
/// <param name="AnalysisSnapshotCacheHits">Number of successful analysis snapshot cache hits.</param>
/// <param name="AnalysisSnapshotCacheMisses">Number of analysis snapshot cache misses.</param>
/// <param name="AnalysisSnapshotCacheStores">Number of analysis snapshot cache writes.</param>
/// <param name="EstimatedAvoidedRequests">Estimated Bitbucket requests avoided by cache hits.</param>
/// <param name="RequestStatistics">Aggregated request statistics by API.</param>
public sealed record BitbucketTelemetrySnapshot(
    bool IsEnabled,
    int TotalRequests,
    int AnalysisSnapshotCacheHits,
    int AnalysisSnapshotCacheMisses,
    int AnalysisSnapshotCacheStores,
    int EstimatedAvoidedRequests,
    IReadOnlyList<BitbucketApiRequestStatistic> RequestStatistics);

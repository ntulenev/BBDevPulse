using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Collects and exposes Bitbucket request and cache telemetry for the current run.
/// </summary>
internal interface IBitbucketTelemetryService
{
    /// <summary>
    /// Tracks a Bitbucket API request.
    /// </summary>
    /// <param name="requestUri">Request URI.</param>
    void TrackRequest(Uri requestUri);

    /// <summary>
    /// Tracks a successful pull request analysis cache hit.
    /// </summary>
    /// <param name="snapshot">Cached snapshot reused for analysis.</param>
    void TrackAnalysisSnapshotCacheHit(PullRequestAnalysisSnapshot snapshot);

    /// <summary>
    /// Tracks a pull request analysis cache miss.
    /// </summary>
    void TrackAnalysisSnapshotCacheMiss();

    /// <summary>
    /// Tracks a persisted pull request analysis cache snapshot.
    /// </summary>
    void TrackAnalysisSnapshotCacheStore();

    /// <summary>
    /// Returns the telemetry snapshot for the current run.
    /// </summary>
    /// <returns>Current telemetry snapshot.</returns>
    BitbucketTelemetrySnapshot GetSnapshot();
}

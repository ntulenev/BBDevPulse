namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized pull request analysis snapshot.
/// </summary>
internal sealed class CachedPullRequestAnalysisSnapshot
{
    public IReadOnlyList<CachedPullRequestActivity>? Activities { get; init; }

    public IReadOnlyList<CachedPullRequestCommitInfo>? CorrectionCommits { get; init; }

    public CachedPullRequestSizeSummary? SizeSummary { get; init; }

    public IReadOnlyList<CachedDeveloperCommitActivity>? CommitActivities { get; init; }

    public bool HasEnrichment { get; init; }
}

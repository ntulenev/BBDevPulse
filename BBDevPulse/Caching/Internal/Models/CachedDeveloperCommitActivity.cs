namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized detailed correction commit activity.
/// </summary>
internal sealed class CachedDeveloperCommitActivity
{
    public string Repository { get; init; } = string.Empty;

    public string RepositorySlug { get; init; } = string.Empty;

    public int PullRequestId { get; init; }

    public string CommitHash { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset Date { get; init; }

    public CachedPullRequestSizeSummary SizeSummary { get; init; } = null!;
}

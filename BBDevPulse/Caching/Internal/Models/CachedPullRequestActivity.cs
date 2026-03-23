namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized pull request activity.
/// </summary>
internal sealed class CachedPullRequestActivity
{
    public DateTimeOffset? ActivityDate { get; init; }

    public DateTimeOffset? MergeDate { get; init; }

    public CachedDeveloperIdentity? Actor { get; init; }

    public CachedActivityComment? Comment { get; init; }

    public CachedActivityApproval? Approval { get; init; }
}

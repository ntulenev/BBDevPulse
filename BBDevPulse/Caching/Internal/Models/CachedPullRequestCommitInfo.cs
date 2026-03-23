namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized correction commit.
/// </summary>
internal sealed class CachedPullRequestCommitInfo
{
    public string Hash { get; init; } = string.Empty;

    public DateTimeOffset Date { get; init; }

    public string Message { get; init; } = string.Empty;
}

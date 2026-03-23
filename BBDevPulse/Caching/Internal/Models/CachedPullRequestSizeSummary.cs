namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized size summary.
/// </summary>
internal sealed class CachedPullRequestSizeSummary
{
    public int FilesChanged { get; init; }

    public int LinesAdded { get; init; }

    public int LinesRemoved { get; init; }
}

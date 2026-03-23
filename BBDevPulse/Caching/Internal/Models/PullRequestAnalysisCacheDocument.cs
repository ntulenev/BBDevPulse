namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized pull request analysis cache document.
/// </summary>
internal sealed class PullRequestAnalysisCacheDocument
{
    /// <summary>
    /// Cache document version.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Serialized snapshot payload.
    /// </summary>
    public CachedPullRequestAnalysisSnapshot? Snapshot { get; init; }
}

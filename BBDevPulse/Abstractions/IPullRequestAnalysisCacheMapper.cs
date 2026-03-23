using BBDevPulse.Caching.Internal.Models;
using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Maps pull request analysis snapshots to and from cache models.
/// </summary>
internal interface IPullRequestAnalysisCacheMapper
{
    /// <summary>
    /// Maps a domain snapshot into a cache document payload.
    /// </summary>
    /// <param name="snapshot">Domain snapshot.</param>
    /// <returns>Cache payload.</returns>
    CachedPullRequestAnalysisSnapshot Map(PullRequestAnalysisSnapshot snapshot);

    /// <summary>
    /// Tries to map a cached snapshot into a domain snapshot.
    /// </summary>
    /// <param name="snapshot">Cached snapshot.</param>
    /// <param name="mappedSnapshot">Mapped domain snapshot.</param>
    /// <returns><see langword="true"/> when mapping succeeded.</returns>
    bool TryMap(CachedPullRequestAnalysisSnapshot snapshot, out PullRequestAnalysisSnapshot mappedSnapshot);
}

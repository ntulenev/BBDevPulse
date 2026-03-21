using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Stores and retrieves pull request commit hash ranges.
/// </summary>
internal interface IPullRequestCommitRangeCache
{
    /// <summary>
    /// Attempts to read cached source and destination commit hashes for a pull request.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="repoSlug">Repository slug.</param>
    /// <param name="pullRequestId">Pull request identifier.</param>
    /// <param name="sourceCommitHash">Resolved source commit hash.</param>
    /// <param name="destinationCommitHash">Resolved destination commit hash.</param>
    /// <returns><see langword="true"/> when a cached range exists.</returns>
    bool TryGet(
        Workspace workspace,
        RepoSlug repoSlug,
        int pullRequestId,
        out string? sourceCommitHash,
        out string? destinationCommitHash);

    /// <summary>
    /// Stores source and destination commit hashes for a pull request when both are available.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="repoSlug">Repository slug.</param>
    /// <param name="pullRequestId">Pull request identifier.</param>
    /// <param name="sourceCommitHash">Source commit hash.</param>
    /// <param name="destinationCommitHash">Destination commit hash.</param>
    void Store(
        Workspace workspace,
        RepoSlug repoSlug,
        int pullRequestId,
        string? sourceCommitHash,
        string? destinationCommitHash);
}

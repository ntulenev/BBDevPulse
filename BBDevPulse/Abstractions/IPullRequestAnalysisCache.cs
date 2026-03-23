using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Stores pull request analysis snapshots between application runs.
/// </summary>
internal interface IPullRequestAnalysisCache
{
    /// <summary>
    /// Attempts to read a cached snapshot for the specified pull request and parameter set.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="repoSlug">Repository slug.</param>
    /// <param name="pullRequestId">Pull request identifier.</param>
    /// <param name="pullRequestFingerprint">Current pull request fingerprint.</param>
    /// <param name="parametersFingerprint">Current analysis parameters fingerprint.</param>
    /// <param name="snapshot">Cached snapshot when available.</param>
    /// <returns><see langword="true"/> when a valid snapshot exists.</returns>
    bool TryGet(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        string pullRequestFingerprint,
        string parametersFingerprint,
        out PullRequestAnalysisSnapshot snapshot);

    /// <summary>
    /// Stores a snapshot for the specified pull request and parameter set.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="repoSlug">Repository slug.</param>
    /// <param name="pullRequestId">Pull request identifier.</param>
    /// <param name="pullRequestFingerprint">Current pull request fingerprint.</param>
    /// <param name="parametersFingerprint">Current analysis parameters fingerprint.</param>
    /// <param name="snapshot">Snapshot to persist.</param>
    void Store(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        string pullRequestFingerprint,
        string parametersFingerprint,
        PullRequestAnalysisSnapshot snapshot);
}

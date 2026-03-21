using System.Collections.Concurrent;
using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.API;

/// <summary>
/// In-memory cache for pull request commit hash ranges.
/// </summary>
internal sealed class PullRequestCommitRangeCache : IPullRequestCommitRangeCache
{
    /// <inheritdoc />
    public bool TryGet(
        Workspace workspace,
        RepoSlug repoSlug,
        int pullRequestId,
        out string? sourceCommitHash,
        out string? destinationCommitHash)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);

        if (_pullRequestCommitRanges.TryGetValue(BuildCacheKey(workspace, repoSlug, pullRequestId), out var commitRange))
        {
            sourceCommitHash = commitRange.SourceCommitHash;
            destinationCommitHash = commitRange.DestinationCommitHash;
            return true;
        }

        sourceCommitHash = null;
        destinationCommitHash = null;
        return false;
    }

    /// <inheritdoc />
    public void Store(
        Workspace workspace,
        RepoSlug repoSlug,
        int pullRequestId,
        string? sourceCommitHash,
        string? destinationCommitHash)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);

        if (string.IsNullOrWhiteSpace(sourceCommitHash) ||
            string.IsNullOrWhiteSpace(destinationCommitHash))
        {
            return;
        }

        _pullRequestCommitRanges[BuildCacheKey(workspace, repoSlug, pullRequestId)] =
            new PullRequestCommitRange(sourceCommitHash, destinationCommitHash);
    }

    private static string BuildCacheKey(Workspace workspace, RepoSlug repoSlug, int pullRequestId) =>
        $"{workspace.Value}/{repoSlug.Value}/{pullRequestId.ToString(CultureInfo.InvariantCulture)}";

    private readonly ConcurrentDictionary<string, PullRequestCommitRange> _pullRequestCommitRanges = new(StringComparer.Ordinal);

    private readonly record struct PullRequestCommitRange(string SourceCommitHash, string DestinationCommitHash);
}

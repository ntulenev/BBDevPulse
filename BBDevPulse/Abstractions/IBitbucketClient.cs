using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Defines Bitbucket API operations used by the application.
/// </summary>
public interface IBitbucketClient
{
    /// <summary>
    /// Fetches the current authenticated Bitbucket user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authenticated user.</returns>
    Task<AuthUser> GetCurrentUserAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Fetches all repositories for a workspace.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="onPage">Optional callback invoked for each page index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All repositories across pages.</returns>
    IAsyncEnumerable<Repository> GetRepositoriesAsync(
        Workspace workspace,
        Action<int>? onPage,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fetches pull requests for a repository.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="repoSlug">Repository slug.</param>
    /// <param name="shouldStop">Predicate that stops pagination when true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pull requests up to the stop condition.</returns>
    IAsyncEnumerable<PullRequest> GetPullRequestsAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        Func<PullRequest, bool> shouldStop,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fetches pull request activity events.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="repoSlug">Repository slug.</param>
    /// <param name="pullRequestId">Pull request identifier.</param>
    /// <param name="shouldStop">Predicate that stops pagination when true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Activity entries up to the stop condition.</returns>
    IAsyncEnumerable<PullRequestActivity> GetPullRequestActivityAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        Func<PullRequestActivity, bool> shouldStop,
        CancellationToken cancellationToken);
}

using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Builds Bitbucket pull request list URIs for repository analysis.
/// </summary>
internal interface IPullRequestsUriBuilder
{
    /// <summary>
    /// Builds the pull request list URI for the provided repository.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="repoSlug">Repository slug.</param>
    /// <returns>Relative URI for the pull request list endpoint.</returns>
    Uri Build(Workspace workspace, RepoSlug repoSlug);
}

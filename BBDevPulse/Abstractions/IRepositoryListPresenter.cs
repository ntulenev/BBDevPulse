using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Presents repository fetching and listing.
/// </summary>
public interface IRepositoryListPresenter
{
    /// <summary>
    /// Fetches repositories while showing progress UI.
    /// </summary>
    /// <param name="fetchRepositories">Function to retrieve repositories with a per-page callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Repository list.</returns>
    Task<IReadOnlyList<Repository>> FetchRepositoriesAsync(
        Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetchRepositories,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renders the repository filter result table.
    /// </summary>
    /// <param name="repositories">Filtered repositories.</param>
    /// <param name="searchMode">Repository search mode.</param>
    /// <param name="filter">Filter text for repository names.</param>
    /// <param name="repoList">Explicit repository list filter.</param>
    void RenderRepositoryTable(
        IReadOnlyCollection<Repository> repositories,
        RepoSearchMode searchMode,
        RepoNameFilter filter,
        IReadOnlyList<RepoName> repoList);
}

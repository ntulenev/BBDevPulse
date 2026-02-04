using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Defines presentation responsibilities for reporting output.
/// </summary>
public interface IReportPresenter
{
    /// <summary>
    /// Displays authentication status using the provided user fetcher.
    /// </summary>
    /// <param name="fetchUser">Function to retrieve the authenticated user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AnnounceAuthAsync(Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken cancellationToken);

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
    /// Runs the repository analysis with progress UI.
    /// </summary>
    /// <param name="repositories">Repositories to analyze.</param>
    /// <param name="analyzeRepository">Callback invoked for each repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AnalyzeRepositoriesAsync(
        IReadOnlyList<Repository> repositories,
        Func<Repository, CancellationToken, Task> analyzeRepository,
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

    /// <summary>
    /// Renders branch filter information.
    /// </summary>
    /// <param name="branchList">Branch names to filter by.</param>
    void RenderBranchFilterInfo(IReadOnlyList<BranchName> branchList);

    /// <summary>
    /// Renders the pull request table.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    /// <param name="filterDate">Filter cutoff date.</param>
    void RenderPullRequestTable(
        ReportData reportData,
        DateTimeOffset filterDate);

    /// <summary>
    /// Renders merge time statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderMergeTimeStats(ReportData reportData);

    /// <summary>
    /// Renders TTFR statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderTtfrStats(ReportData reportData);

    /// <summary>
    /// Renders per-developer statistics.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    /// <param name="filterDate">Filter cutoff date.</param>
    void RenderDeveloperStatsTable(
        ReportData reportData,
        DateTimeOffset filterDate);
}

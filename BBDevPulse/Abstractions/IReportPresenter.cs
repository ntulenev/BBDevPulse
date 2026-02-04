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
    /// <param name="parameters">Report parameters.</param>
    void RenderRepositoryTable(
        IReadOnlyCollection<Repository> repositories,
        ReportParameters parameters);

    /// <summary>
    /// Renders branch filter information.
    /// </summary>
    /// <param name="parameters">Report parameters.</param>
    void RenderBranchFilterInfo(ReportParameters parameters);

    /// <summary>
    /// Renders the full report output.
    /// </summary>
    /// <param name="reportData">Report data.</param>
    void RenderReport(ReportData reportData);
}

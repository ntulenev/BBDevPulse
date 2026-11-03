using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Defines presentation responsibilities for reporting output.
/// </summary>
public interface IReportPresenter
{
    /// <summary>
    /// Shows the effective report settings before data collection starts.
    /// </summary>
    /// <param name="parameters">Resolved report parameters.</param>
    void RenderRunSettings(ReportParameters parameters);

    /// <summary>
    /// Waits for the user to approve starting Bitbucket data collection.
    /// </summary>
    /// <returns>True when the user confirms the run.</returns>
    bool ConfirmStart();

    /// <summary>
    /// Lets the user choose one of the teams found in the people CSV, or all teams.
    /// </summary>
    /// <param name="teams">Distinct team names.</param>
    /// <returns>The selected team, or null for no filter.</returns>
    string? SelectTeamFilter(IReadOnlyList<string> teams);

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

    /// <summary>
    /// Renders Bitbucket telemetry summary.
    /// </summary>
    /// <param name="telemetrySnapshot">Telemetry snapshot.</param>
    void RenderTelemetrySummary(BitbucketTelemetrySnapshot telemetrySnapshot);
}

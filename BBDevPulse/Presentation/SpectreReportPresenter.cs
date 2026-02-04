using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console implementation of the report presenter.
/// </summary>
public sealed class SpectreReportPresenter : IReportPresenter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreReportPresenter"/> class.
    /// </summary>
    /// <param name="authPresenter">Authentication presenter.</param>
    /// <param name="repositoryListPresenter">Repository list presenter.</param>
    /// <param name="repositoryAnalysisPresenter">Repository analysis presenter.</param>
    /// <param name="branchFilterPresenter">Branch filter presenter.</param>
    /// <param name="pullRequestReportPresenter">Pull request report presenter.</param>
    /// <param name="statisticsPresenter">Statistics presenter.</param>
    public SpectreReportPresenter(
        IAuthPresenter authPresenter,
        IRepositoryListPresenter repositoryListPresenter,
        IRepositoryAnalysisPresenter repositoryAnalysisPresenter,
        IBranchFilterPresenter branchFilterPresenter,
        IPullRequestReportPresenter pullRequestReportPresenter,
        IStatisticsPresenter statisticsPresenter)
    {
        ArgumentNullException.ThrowIfNull(authPresenter);
        ArgumentNullException.ThrowIfNull(repositoryListPresenter);
        ArgumentNullException.ThrowIfNull(repositoryAnalysisPresenter);
        ArgumentNullException.ThrowIfNull(branchFilterPresenter);
        ArgumentNullException.ThrowIfNull(pullRequestReportPresenter);
        ArgumentNullException.ThrowIfNull(statisticsPresenter);
        _authPresenter = authPresenter;
        _repositoryListPresenter = repositoryListPresenter;
        _repositoryAnalysisPresenter = repositoryAnalysisPresenter;
        _branchFilterPresenter = branchFilterPresenter;
        _pullRequestReportPresenter = pullRequestReportPresenter;
        _statisticsPresenter = statisticsPresenter;
    }

    /// <inheritdoc />
    public Task AnnounceAuthAsync(Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken cancellationToken) =>
        _authPresenter.AnnounceAuthAsync(fetchUser, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Repository>> FetchRepositoriesAsync(
        Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetchRepositories,
        CancellationToken cancellationToken) =>
        _repositoryListPresenter.FetchRepositoriesAsync(fetchRepositories, cancellationToken);

    /// <inheritdoc />
    public Task AnalyzeRepositoriesAsync(
        IReadOnlyList<Repository> repositories,
        Func<Repository, CancellationToken, Task> analyzeRepository,
        CancellationToken cancellationToken) =>
        _repositoryAnalysisPresenter.AnalyzeRepositoriesAsync(repositories, analyzeRepository, cancellationToken);

    /// <inheritdoc />
    public void RenderRepositoryTable(
        IReadOnlyCollection<Repository> repositories,
        ReportParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _repositoryListPresenter.RenderRepositoryTable(
            repositories,
            parameters.RepoSearchMode,
            parameters.RepoNameFilter,
            parameters.RepoNameList);
    }

    /// <inheritdoc />
    public void RenderBranchFilterInfo(ReportParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _branchFilterPresenter.RenderBranchFilterInfo(parameters.BranchNameList);
    }

    /// <inheritdoc />
    public void RenderReport(ReportData reportData)
    {
        ArgumentNullException.ThrowIfNull(reportData);

        reportData.SortReportsByCreatedOn();

        var filterDate = reportData.Parameters.FilterDate;

        _pullRequestReportPresenter.RenderPullRequestTable(reportData, filterDate);
        _statisticsPresenter.RenderMergeTimeStats(reportData);
        _statisticsPresenter.RenderTtfrStats(reportData);
        _statisticsPresenter.RenderDeveloperStatsTable(reportData, filterDate);
    }

    private readonly IAuthPresenter _authPresenter;
    private readonly IRepositoryListPresenter _repositoryListPresenter;
    private readonly IRepositoryAnalysisPresenter _repositoryAnalysisPresenter;
    private readonly IBranchFilterPresenter _branchFilterPresenter;
    private readonly IPullRequestReportPresenter _pullRequestReportPresenter;
    private readonly IStatisticsPresenter _statisticsPresenter;
}

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Logic;

/// <summary>
/// Coordinates Bitbucket data collection and report generation.
/// </summary>
internal sealed class ReportRunner : IReportRunner
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRunner"/> class.
    /// </summary>
    /// <param name="client">Bitbucket API client.</param>
    /// <param name="analyzer">Pull request analyzer.</param>
    /// <param name="presenter">Report presenter.</param>
    /// <param name="options">Bitbucket options.</param>
    public ReportRunner(
        IBitbucketClient client,
        IPullRequestAnalyzer analyzer,
        IReportPresenter presenter,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _analyzer = analyzer;
        _presenter = presenter;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var filterDate = DateTimeOffset.UtcNow.AddDays(-_options.Days);
        var workspace = new Workspace(_options.Workspace);
        var repoNameFilter = new RepoNameFilter(_options.RepoNameFilter);
        var repoNameList = (_options.RepoNameList ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => new RepoName(entry))
            .ToList();
        var repoSearchMode = _options.RepoSearchMode;
        var prTimeFilterMode = _options.PrTimeFilterMode;
        var branchNameList = (_options.BranchNameList ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => new BranchName(entry))
            .ToList();
        var reports = new List<PullRequestReport>();
        var developerStats = new Dictionary<DeveloperKey, DeveloperStats>();

        await _presenter.AnnounceAuthAsync(
                _client.GetCurrentUserAsync,
                cancellationToken)
            .ConfigureAwait(false);

        var repositories = await _presenter.FetchRepositoriesAsync(
                (onPage, token) => _client.GetRepositoriesAsync(workspace, onPage, token),
                cancellationToken)
            .ConfigureAwait(false);

        var filteredRepos = repositories
            .Where(repo => repo.MatchesFilter(repoSearchMode, repoNameFilter, repoNameList))
            .ToList();

        _presenter.RenderRepositoryTable(filteredRepos, repoSearchMode, repoNameFilter, repoNameList);
        _presenter.RenderBranchFilterInfo(branchNameList);

        await _presenter.AnalyzeRepositoriesAsync(filteredRepos, async (repo, token) =>
        {
            await _analyzer.AnalyzeAsync(
                workspace,
                repo,
                filterDate,
                prTimeFilterMode,
                branchNameList,
                reports,
                developerStats,
                token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        var sortedReports = reports
            .OrderBy(r => r.CreatedOn)
            .ToList();

        _presenter.RenderPullRequestTable(sortedReports, filterDate);
        _presenter.RenderMergeTimeStats(sortedReports);
        _presenter.RenderTtfrStats(sortedReports);
        _presenter.RenderDeveloperStatsTable(developerStats, filterDate);
    }

    private readonly IBitbucketClient _client;
    private readonly IPullRequestAnalyzer _analyzer;
    private readonly IReportPresenter _presenter;
    private readonly BitbucketOptions _options;
}

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
        _parameters = options.Value.CreateReportParameters();
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await BuildAuthAsync(cancellationToken).ConfigureAwait(false);
        var filteredRepos = await BuildRepositoriesAsync(cancellationToken).ConfigureAwait(false);
        await BuildReportsAsync(filteredRepos, cancellationToken).ConfigureAwait(false);
    }

    private async Task BuildAuthAsync(CancellationToken cancellationToken)
    {
        await _presenter.AnnounceAuthAsync(
       _client.GetCurrentUserAsync, cancellationToken)
              .ConfigureAwait(false);
    }

    private async Task<List<Repository>> BuildRepositoriesAsync(CancellationToken cancellationToken)
    {
        var repositories = await _presenter.FetchRepositoriesAsync(
        (onPage, token) => _client.GetRepositoriesAsync(_parameters.Workspace, onPage, token),
        cancellationToken)
        .ConfigureAwait(false);
        var filteredRepos = repositories
            .Where(repo => repo.MatchesFilter(_parameters)).ToList();
        _presenter.RenderRepositoryTable(
           filteredRepos,
           _parameters);
        _presenter.RenderBranchFilterInfo(_parameters);
        return filteredRepos;
    }

    private async Task BuildReportsAsync(List<Repository> filteredRepos, CancellationToken cancellationToken)
    {
        var reportData = new ReportData(_parameters);
        await _presenter.AnalyzeRepositoriesAsync(filteredRepos, async (repo, token) =>
        {
            await _analyzer.AnalyzeAsync(
                repo,
                reportData,
                token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        _presenter.RenderReport(reportData);
    }

    private readonly IBitbucketClient _client;
    private readonly IPullRequestAnalyzer _analyzer;
    private readonly IReportPresenter _presenter;
    private readonly ReportParameters _parameters;
}

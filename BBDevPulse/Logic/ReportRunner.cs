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
    /// <param name="pdfReportRenderer">PDF report renderer.</param>
    /// <param name="peopleCsvProvider">People CSV provider.</param>
    /// <param name="options">Bitbucket options.</param>
    public ReportRunner(
        IBitbucketClient client,
        IPullRequestAnalyzer analyzer,
        IReportPresenter presenter,
        IPdfReportRenderer pdfReportRenderer,
        IPeopleCsvProvider peopleCsvProvider,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(pdfReportRenderer);
        ArgumentNullException.ThrowIfNull(peopleCsvProvider);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _analyzer = analyzer;
        _presenter = presenter;
        _pdfReportRenderer = pdfReportRenderer;
        _peopleCsvProvider = peopleCsvProvider;
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
        if (reportData.DeveloperStats.Count > 0)
        {
            EnrichDeveloperStatsFromPeopleCsv(reportData, _peopleCsvProvider.GetPeopleByDisplayName());
        }
        _presenter.RenderReport(reportData);
        _pdfReportRenderer.RenderReport(reportData);
    }

    private static void EnrichDeveloperStatsFromPeopleCsv(
        ReportData reportData,
        Dictionary<DisplayName, PersonCsvRow> peopleByName)
    {
        if (reportData.DeveloperStats.Count == 0 || peopleByName.Count == 0)
        {
            return;
        }

        var peopleByNameValue = peopleByName
            .ToDictionary(entry => entry.Key.Value, entry => entry.Value, StringComparer.Ordinal);

        foreach (var stat in reportData.DeveloperStats.Values)
        {
            if (peopleByNameValue.TryGetValue(stat.DisplayName.Value, out var person))
            {
                stat.Grade = person.Grade;
                stat.Department = person.Department;
            }
        }
    }

    private readonly IBitbucketClient _client;
    private readonly IPullRequestAnalyzer _analyzer;
    private readonly IReportPresenter _presenter;
    private readonly IPdfReportRenderer _pdfReportRenderer;
    private readonly IPeopleCsvProvider _peopleCsvProvider;
    private readonly ReportParameters _parameters;
}

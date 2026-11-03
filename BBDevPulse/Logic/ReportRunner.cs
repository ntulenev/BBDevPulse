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
    /// <param name="htmlReportRenderer">HTML report renderer.</param>
    /// <param name="pdfReportRenderer">PDF report renderer.</param>
    /// <param name="peopleCsvProvider">People CSV provider.</param>
    /// <param name="telemetryService">Telemetry service.</param>
    /// <param name="options">Bitbucket options.</param>
    public ReportRunner(
        IBitbucketClient client,
        IPullRequestAnalyzer analyzer,
        IReportPresenter presenter,
        IHtmlReportRenderer htmlReportRenderer,
        IPdfReportRenderer pdfReportRenderer,
        IPeopleCsvProvider peopleCsvProvider,
        IBitbucketTelemetryService telemetryService,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(htmlReportRenderer);
        ArgumentNullException.ThrowIfNull(pdfReportRenderer);
        ArgumentNullException.ThrowIfNull(peopleCsvProvider);
        ArgumentNullException.ThrowIfNull(telemetryService);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _analyzer = analyzer;
        _presenter = presenter;
        _htmlReportRenderer = htmlReportRenderer;
        _pdfReportRenderer = pdfReportRenderer;
        _peopleCsvProvider = peopleCsvProvider;
        _telemetryService = telemetryService;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var peopleByName = await _peopleCsvProvider.GetPeopleByDisplayNameAsync(cancellationToken).ConfigureAwait(false);
        var teams = peopleByName.Values
            .Select(static person => person.Department.Trim())
            .Where(static team => !string.IsNullOrWhiteSpace(team) &&
                !string.Equals(team, DeveloperStats.NOT_AVAILABLE, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static team => team, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var teamFilter = teams.Count == 0 ? null : _presenter.SelectTeamFilter(teams);
        var parameters = _options.CreateReportParameters(teamFilter);
        _presenter.RenderRunSettings(parameters);
        await BuildAuthAsync(cancellationToken).ConfigureAwait(false);
        var filteredRepos = await BuildRepositoriesAsync(parameters, cancellationToken).ConfigureAwait(false);
        await BuildReportsAsync(filteredRepos, parameters, peopleByName, cancellationToken).ConfigureAwait(false);
    }

    private async Task BuildAuthAsync(CancellationToken cancellationToken)
    {
        await _presenter.AnnounceAuthAsync(
       _client.GetCurrentUserAsync, cancellationToken)
              .ConfigureAwait(false);
    }

    private async Task<List<Repository>> BuildRepositoriesAsync(ReportParameters parameters, CancellationToken cancellationToken)
    {
        var repositories = await _presenter.FetchRepositoriesAsync(
        (onPage, token) => _client.GetRepositoriesAsync(parameters.Workspace, onPage, token),
        cancellationToken)
        .ConfigureAwait(false);
        var filteredRepos = repositories
            .Where(repo => repo.MatchesFilter(parameters)).ToList();
        _presenter.RenderRepositoryTable(
           filteredRepos,
           parameters);
        _presenter.RenderBranchFilterInfo(parameters);
        return filteredRepos;
    }

    private async Task BuildReportsAsync(List<Repository> filteredRepos, ReportParameters parameters,
        Dictionary<DisplayName, PersonCsvRow> peopleByName, CancellationToken cancellationToken)
    {
        var reportData = new ReportData(parameters, peopleByName);
        await _presenter.AnalyzeRepositoriesAsync(filteredRepos, async (repo, token) =>
        {
            await _analyzer.AnalyzeAsync(
                repo,
                reportData,
                token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (reportData.DeveloperStats.Count > 0)
        {
            EnrichDeveloperStatsFromPeopleCsv(reportData, peopleByName);
        }
        _presenter.RenderReport(reportData);
        _presenter.RenderTelemetrySummary(_telemetryService.GetSnapshot());
        await _htmlReportRenderer.RenderReportAsync(reportData, cancellationToken).ConfigureAwait(false);
        await _pdfReportRenderer.RenderReportAsync(reportData, cancellationToken).ConfigureAwait(false);
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
    private readonly IHtmlReportRenderer _htmlReportRenderer;
    private readonly IPdfReportRenderer _pdfReportRenderer;
    private readonly IPeopleCsvProvider _peopleCsvProvider;
    private readonly IBitbucketTelemetryService _telemetryService;
    private readonly BitbucketOptions _options;
}

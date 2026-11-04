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
    /// <param name="publisher">Publishes enabled report files.</param>
    /// <param name="peopleCsvProvider">People CSV provider.</param>
    /// <param name="telemetryService">Telemetry service.</param>
    /// <param name="options">Bitbucket options.</param>
    public ReportRunner(
        IBitbucketClient client,
        IPullRequestAnalyzer analyzer,
        IReportPresenter presenter,
        ReportFilePublisher publisher,
        IPeopleCsvProvider peopleCsvProvider,
        IBitbucketTelemetryService telemetryService,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(peopleCsvProvider);
        ArgumentNullException.ThrowIfNull(telemetryService);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _analyzer = analyzer;
        _presenter = presenter;
        _publisher = publisher;
        _peopleCsvProvider = peopleCsvProvider;
        _telemetryService = telemetryService;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var peopleByName = await _peopleCsvProvider.GetPeopleByDisplayNameAsync(cancellationToken).ConfigureAwait(false);
        var teams = TeamFilterCatalog.Create(peopleByName);
        var teamFilter = teams.Count == 0 ? null : _presenter.SelectTeamFilter(teams, peopleByName.Values.Sum(static row => row.Rows.Count()));
        var parameters = _options.CreateReportParameters(teamFilter);
        var htmlOptions = _options.Html ?? new HtmlOptions();
        var pdfOptions = _options.Pdf ?? new PdfOptions();
        var htmlFileName = htmlOptions.Enabled ? Path.GetFileName(htmlOptions.ResolveOutputPath(teamFilter)) : null;
        var pdfFileName = pdfOptions.Enabled ? Path.GetFileName(pdfOptions.ResolveOutputPath(teamFilter)) : null;
        _presenter.RenderRunSettings(parameters, _options.Username, htmlFileName, pdfFileName);
        if (!_presenter.ConfirmStart())
        {
            return;
        }

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
        var accumulator = new ReportAccumulator(parameters, peopleByName);
        var coordinator = new RepositoryAnalysisCoordinator(_options.RepositoryConcurrency);
        await _presenter.RunWithRepositoryProgressAsync(filteredRepos.Count,
            (progress, token) => coordinator.RunAsync(filteredRepos,
                (repo, itemToken) => _analyzer.AnalyzeAsync(repo, accumulator, itemToken),
                progress, token), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        accumulator.CompleteAnalysis(cancellationToken);
        accumulator.EnrichDevelopers();
        var report = accumulator.Build(cancellationToken);
        _presenter.RenderReport(report);
        _presenter.RenderTelemetrySummary(_telemetryService.GetSnapshot());
        await _publisher.PublishAsync(report, cancellationToken).ConfigureAwait(false);
    }

    private readonly IBitbucketClient _client;
    private readonly IPullRequestAnalyzer _analyzer;
    private readonly IReportPresenter _presenter;
    private readonly ReportFilePublisher _publisher;
    private readonly IPeopleCsvProvider _peopleCsvProvider;
    private readonly IBitbucketTelemetryService _telemetryService;
    private readonly BitbucketOptions _options;
}

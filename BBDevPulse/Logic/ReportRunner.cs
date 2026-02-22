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
    /// <param name="options">Bitbucket options.</param>
    public ReportRunner(
        IBitbucketClient client,
        IPullRequestAnalyzer analyzer,
        IReportPresenter presenter,
        IPdfReportRenderer pdfReportRenderer,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(pdfReportRenderer);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _analyzer = analyzer;
        _presenter = presenter;
        _pdfReportRenderer = pdfReportRenderer;
        _parameters = options.Value.CreateReportParameters();
        _peopleCsvPath = string.IsNullOrWhiteSpace(options.Value.PeopleCsvPath)
            ? null
            : options.Value.PeopleCsvPath.Trim();
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
        EnrichDeveloperStatsFromPeopleCsv(reportData);
        _presenter.RenderReport(reportData);
        _pdfReportRenderer.RenderReport(reportData);
    }

    private void EnrichDeveloperStatsFromPeopleCsv(ReportData reportData)
    {
        if (string.IsNullOrWhiteSpace(_peopleCsvPath) || reportData.DeveloperStats.Count == 0)
        {
            return;
        }

        var peopleByName = LoadPeopleFromCsv(_peopleCsvPath);
        foreach (var stat in reportData.DeveloperStats.Values)
        {
            if (peopleByName.TryGetValue(stat.DisplayName.Value, out var person))
            {
                stat.Grade = person.Grade;
                stat.Department = person.Department;
            }
        }
    }

    private static Dictionary<string, PersonCsvRow> LoadPeopleFromCsv(string peopleCsvPath)
    {
        if (!File.Exists(peopleCsvPath))
        {
            throw new FileNotFoundException($"People CSV file '{peopleCsvPath}' was not found.", peopleCsvPath);
        }

        var peopleByName = new Dictionary<string, PersonCsvRow>(StringComparer.Ordinal);
        var lineNumber = 0;
        foreach (var line in File.ReadLines(peopleCsvPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmedLine = line.Trim();
            if (lineNumber == 1 && trimmedLine.Equals("Name;Grade;Department", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmedLine.Split(';');
            if (parts.Length != 3)
            {
                throw new FormatException(
                    $"Invalid people CSV format at line {lineNumber}. Expected 'Name;Grade;Department'.");
            }

            var name = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var grade = string.IsNullOrWhiteSpace(parts[1]) ? DeveloperStats.NotAvailable : parts[1].Trim();
            var department = string.IsNullOrWhiteSpace(parts[2]) ? DeveloperStats.NotAvailable : parts[2].Trim();
            peopleByName[name] = new PersonCsvRow(grade, department);
        }

        return peopleByName;
    }

    private readonly IBitbucketClient _client;
    private readonly IPullRequestAnalyzer _analyzer;
    private readonly IReportPresenter _presenter;
    private readonly IPdfReportRenderer _pdfReportRenderer;
    private readonly ReportParameters _parameters;
    private readonly string? _peopleCsvPath;

    private readonly record struct PersonCsvRow(string Grade, string Department);
}

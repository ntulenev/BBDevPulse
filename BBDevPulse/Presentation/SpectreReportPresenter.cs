using BBDevPulse.Abstractions;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console implementation of the report presenter.
/// </summary>
public sealed class SpectreReportPresenter : IReportPresenter
{
    /// <inheritdoc />
    public void RenderRunSettings(ReportParameters parameters, string username, string? htmlFileName, string? pdfFileName) =>
        SpectreRunSettingsPresenter.Render(parameters, username, htmlFileName, pdfFileName);

    /// <inheritdoc />
    public void RenderDemoSettings(ReportParameters parameters, string htmlFileName, string pdfFileName) =>
        SpectreRunSettingsPresenter.Render(parameters, "synthetic-user", htmlFileName, pdfFileName, demoMode: true);

    /// <inheritdoc />
    public bool ConfirmDemoStart() => AnsiConsole.Confirm("Render the synthetic demo report?", defaultValue: false);

    /// <inheritdoc />
    public bool ConfirmStart() => AnsiConsole.Confirm("Start loading data from Bitbucket?", defaultValue: false);

    /// <inheritdoc />
    public string? SelectTeamFilter(IReadOnlyList<TeamFilterOption> teams, int totalPeople)
    {
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentOutOfRangeException.ThrowIfNegative(totalPeople);
        if (teams.Count == 0)
        {
            return null;
        }

        var choices = new List<TeamChoice> { new("Do not apply team filter", null) };
        choices.AddRange(teams.Select(team => new TeamChoice(
            $"{team.Name} ({CountLabel(team.Members.Count)})", team)));
        var prompt = new SelectionPrompt<TeamChoice>()
            .Title("Filter report by team?")
            .PageSize(10)
            .UseConverter(choice => Markup.Escape(choice.Label))
            .AddChoices(choices);
        var selected = AnsiConsole.Prompt(prompt).Team;
        if (selected is not null)
        {
            ShowTeamMembers(selected);
        }

        return selected?.Name;
    }

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

        _pullRequestReportPresenter.RenderPullRequestTable(reportData, reportData.Parameters.FilterDate);
        _statisticsPresenter.RenderMergeTimeStats(reportData);
        _statisticsPresenter.RenderTtfrStats(reportData);
        _statisticsPresenter.RenderCorrectionsStats(reportData);
        _statisticsPresenter.RenderPullRequestSizeStats(reportData);
        _statisticsPresenter.RenderPrThroughputStats(reportData);
        _statisticsPresenter.RenderPrsPerDeveloperStats(reportData);
        _statisticsPresenter.RenderCommentsStats(reportData);
        _statisticsPresenter.RenderPeerCommentsStats(reportData);
        _statisticsPresenter.RenderWorstPullRequestsTable(reportData);
        _statisticsPresenter.RenderDeveloperStatsTable(reportData);
        if (reportData.Parameters.ShowAllDetailsForDevelopers)
        {
            _statisticsPresenter.RenderDeveloperDetails(reportData);
        }
    }

    /// <inheritdoc />
    public void RenderTelemetrySummary(BitbucketTelemetrySnapshot telemetrySnapshot)
    {
        ArgumentNullException.ThrowIfNull(telemetrySnapshot);
        _statisticsPresenter.RenderBitbucketTelemetry(telemetrySnapshot);
    }

    private readonly IAuthPresenter _authPresenter;
    private readonly IRepositoryListPresenter _repositoryListPresenter;
    private readonly IRepositoryAnalysisPresenter _repositoryAnalysisPresenter;
    private readonly IBranchFilterPresenter _branchFilterPresenter;
    private readonly IPullRequestReportPresenter _pullRequestReportPresenter;
    private readonly IStatisticsPresenter _statisticsPresenter;

    private sealed record TeamChoice(string Label, TeamFilterOption? Team);

    private sealed record PageChoice(string Label, int? Page);

    private static string CountLabel(int count) => count == 1 ? "1 person" : $"{count} people";

    private static void ShowTeamMembers(TeamFilterOption team)
    {
        const int pageSize = 12;
        var pageCount = (team.Members.Count + pageSize - 1) / pageSize;
        var page = 0;
        while (true)
        {
            AnsiConsole.Write(new Rule($"[bold deepskyblue1]{Markup.Escape(team.Name)} · {CountLabel(team.Members.Count)}[/]")
                .LeftJustified().RuleStyle("grey"));
            AnsiConsole.MarkupLine($"[grey]People in selected team · page {page + 1} of {pageCount}[/]");
            var table = new Table().Border(TableBorder.Rounded).Expand();
            _ = table.AddColumn("Name");
            _ = table.AddColumn("Grade");
            foreach (var member in team.Members.Skip(page * pageSize).Take(pageSize))
            {
                _ = table.AddRow(Markup.Escape(member.Name), Markup.Escape(member.Grade));
            }

            AnsiConsole.Write(table);
            if (pageCount == 1)
            {
                return;
            }

            var choices = new List<PageChoice>();
            if (page > 0)
            {
                choices.Add(new("Previous page", page - 1));
            }

            if (page + 1 < pageCount)
            {
                choices.Add(new("Next page", page + 1));
            }

            choices.Add(new("Done reviewing", null));
            var navigation = new SelectionPrompt<PageChoice>()
                .Title("Review team members")
                .UseConverter(static choice => choice.Label)
                .AddChoices(choices);
            var nextPage = AnsiConsole.Prompt(navigation).Page;
            if (!nextPage.HasValue)
            {
                return;
            }

            page = nextPage.Value;
        }
    }
}

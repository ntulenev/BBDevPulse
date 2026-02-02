using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console implementation of the report presenter.
/// </summary>
public sealed class SpectreReportPresenter : IReportPresenter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreReportPresenter"/> class.
    /// </summary>
    /// <param name="statisticsCalculator">Statistics calculator.</param>
    public SpectreReportPresenter(
        IStatisticsCalculator statisticsCalculator,
        IDateDiffFormatter dateDiffFormatter)
    {
        ArgumentNullException.ThrowIfNull(statisticsCalculator);
        ArgumentNullException.ThrowIfNull(dateDiffFormatter);
        _statisticsCalculator = statisticsCalculator;
        _dateDiffFormatter = dateDiffFormatter;
    }

    /// <inheritdoc />
    public async Task AnnounceAuthAsync(Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fetchUser);
        AnsiConsole.MarkupLine("[grey]Authenticating with Bitbucket...[/]");
        try
        {
            var user = await fetchUser(cancellationToken).ConfigureAwait(false);
            var name = user.DisplayName.Value;
            AnsiConsole.MarkupLine($"[green]Auth succeeded for user:[/] {name}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Auth failed:[/] {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Repository>> FetchRepositoriesAsync(
        Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetchRepositories,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fetchRepositories);
        var repositories = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Loading repositories...", async _ =>
            {
                var result = new List<Repository>();
                await foreach (var repo in fetchRepositories(
                                   page => AnsiConsole.MarkupLine($"[grey]Loading repositories: page {page}[/]"),
                                   cancellationToken).ConfigureAwait(false))
                {
                    result.Add(repo);
                }

                return result;
            })
            .ConfigureAwait(false);

        return [.. repositories];
    }

    /// <inheritdoc />
    public async Task AnalyzeRepositoriesAsync(
        IReadOnlyList<Repository> repositories,
        Func<Repository, CancellationToken, Task> analyzeRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(analyzeRepository);
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
                new ElapsedTimeColumn(),
                new RemainingTimeColumn(),
                new TaskDescriptionColumn())
            .StartAsync(async context =>
            {
                var task = context.AddTask("Analyzing repositories", maxValue: repositories.Count);

                foreach (var repo in repositories)
                {
                    task.Description = $"Analyzing {repo.DisplayName}";
                    await analyzeRepository(repo, cancellationToken).ConfigureAwait(false);
                    task.Increment(1);
                }
            })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void RenderRepositoryTable(
        IReadOnlyCollection<Repository> repositories,
        RepoSearchMode searchMode,
        RepoNameFilter filter,
        IReadOnlyList<RepoName> repoList)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(repoList);
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Repository")
            .AddColumn("Slug");

        var index = 1;
        foreach (var repo in repositories.OrderBy(repo => repo.DisplayName))
        {
            _ = table.AddRow(
                index.ToString(CultureInfo.InvariantCulture),
                repo.DisplayName,
                repo.Slug.Value);
            index++;
        }

        var title = searchMode switch
        {
            RepoSearchMode.FilterFromTheList when repoList.Count > 0
                => $"Repositories (list count: {repoList.Count})",
            RepoSearchMode.SearchByFilter when !string.IsNullOrWhiteSpace(filter.Value)
                => $"Repositories (contains: {filter.Value})",
            _ => "Repositories (all)"
        };

        AnsiConsole.Write(new Rule(title).RuleStyle("grey"));
        AnsiConsole.Write(table);
    }

    /// <inheritdoc />
    public void RenderBranchFilterInfo(IReadOnlyList<BranchName> branchList)
    {
        ArgumentNullException.ThrowIfNull(branchList);
        if (branchList.Count == 0)
        {
            return;
        }

        var joined = string.Join(", ", branchList.Select(branch => branch.Value));
        AnsiConsole.MarkupLine($"[grey]Filtering PRs by target branches:[/] {joined}");
    }

    /// <inheritdoc />
    public void RenderPullRequestTable(
        IReadOnlyCollection<PullRequestReport> reports,
        DateTimeOffset filterDate)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Repository")
            .AddColumn("Author")
            .AddColumn("Target")
            .AddColumn("Created")
            .AddColumn("TTFR")
            .AddColumn("Last Activity")
            .AddColumn("Merged")
            .AddColumn("Rejected")
            .AddColumn("PR Age (days)")
            .AddColumn("Time to Merge")
            .AddColumn("Comments")
            .AddColumn("PR ID");

        var index = 1;
        foreach (var report in reports)
        {
            var timeToMerge = report.MergedOn.HasValue
                ? _dateDiffFormatter.Format(report.CreatedOn, report.MergedOn.Value)
                : "-";
            var prAge = report.State == PullRequestState.Open
                ? (DateTimeOffset.UtcNow - report.CreatedOn).TotalDays.ToString("0.0", CultureInfo.InvariantCulture)
                : "-";
            var ttfr = report.FirstReactionOn.HasValue
                ? _dateDiffFormatter.Format(report.CreatedOn, report.FirstReactionOn.Value)
                : "-";
            var createdCell = report.CreatedOn < filterDate
                ? $"[red]{report.CreatedOn:yyyy-MM-dd}[/]"
                : report.CreatedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _ = table.AddRow(
                index.ToString(CultureInfo.InvariantCulture),
                report.Repository,
                report.Author,
                report.TargetBranch,
                createdCell,
                ttfr,
                report.LastActivity.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                report.MergedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-",
                report.RejectedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-",
                prAge,
                timeToMerge,
                report.Comments.ToString(CultureInfo.InvariantCulture),
                report.Id.Value.ToString(CultureInfo.InvariantCulture)
        );

            index++;
        }

        AnsiConsole.Write(new Rule("Pull Requests").RuleStyle("grey"));
        AnsiConsole.Write(table);
    }

    /// <inheritdoc />
    public void RenderMergeTimeStats(IReadOnlyCollection<PullRequestReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var mergeDays = reports
            .Where(r => r.MergedOn.HasValue)
            .Select(r => (r.MergedOn!.Value - r.CreatedOn).TotalDays)
            .OrderBy(days => days)
            .ToList();

        if (mergeDays.Count == 0)
        {
            AnsiConsole.Write(new Rule("Merge Time Stats").RuleStyle("grey"));
            AnsiConsole.MarkupLine("[yellow]No merged pull requests in the report.[/]");
            return;
        }

        var best = mergeDays.First();
        var longest = mergeDays.Last();
        var median = _statisticsCalculator.Percentile(mergeDays, 50);
        var p75 = _statisticsCalculator.Percentile(mergeDays, 75);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Time");

        _ = table.AddRow("Best Merge Time", _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(best)));
        _ = table.AddRow("Longest Merge Time", _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(longest)));
        _ = table.AddRow("Median", _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(median)));
        _ = table.AddRow("75P", _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(p75)));

        AnsiConsole.Write(new Rule("Merge Time Stats").RuleStyle("grey"));
        AnsiConsole.Write(table);
    }

    /// <inheritdoc />
    public void RenderTtfrStats(IReadOnlyCollection<PullRequestReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var ttfrDays = reports
            .Where(r => r.FirstReactionOn.HasValue)
            .Select(r => (r.FirstReactionOn!.Value - r.CreatedOn).TotalDays)
            .OrderBy(days => days)
            .ToList();

        if (ttfrDays.Count == 0)
        {
            AnsiConsole.Write(new Rule("TTFR Stats").RuleStyle("grey"));
            AnsiConsole.MarkupLine("[yellow]No TTFR data available in the report.[/]");
            return;
        }

        var best = ttfrDays.First();
        var longest = ttfrDays.Last();
        var median = _statisticsCalculator.Percentile(ttfrDays, 50);
        var p75 = _statisticsCalculator.Percentile(ttfrDays, 75);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Time");

        _ = table.AddRow("Best TTFR", _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(best)));
        _ = table.AddRow("Longest TTFR", _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(longest)));
        _ = table.AddRow("Median", _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(median)));
        _ = table.AddRow("75P", _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(p75)));

        AnsiConsole.Write(new Rule("TTFR Stats").RuleStyle("grey"));
        AnsiConsole.Write(table);
    }

    /// <inheritdoc />
    public void RenderDeveloperStatsTable(
        IReadOnlyDictionary<DeveloperKey, DeveloperStats> stats,
        DateTimeOffset filterDate)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Developer")
            .AddColumn("PRs Opened")
            .AddColumn("PRs Merged")
            .AddColumn("Comments")
            .AddColumn("Approvals");

        var index = 1;
        foreach (var stat in stats.Values
            .OrderByDescending(s => s.PrsOpenedSince)
            .ThenBy(s => s.DisplayName.Value, StringComparer.OrdinalIgnoreCase))
        {
            _ = table.AddRow(
                index.ToString(CultureInfo.InvariantCulture),
                stat.DisplayName.Value,
                stat.PrsOpenedSince.ToString(CultureInfo.InvariantCulture),
                stat.PrsMergedAfter.ToString(CultureInfo.InvariantCulture),
                stat.CommentsAfter.ToString(CultureInfo.InvariantCulture),
                stat.ApprovalsAfter.ToString(CultureInfo.InvariantCulture)
            );
            index++;
        }

        AnsiConsole.Write(new Rule($"Developer Stats (since {filterDate:yyyy-MM-dd})").RuleStyle("grey"));
        AnsiConsole.Write(table);
    }

    private readonly IStatisticsCalculator _statisticsCalculator;
    private readonly IDateDiffFormatter _dateDiffFormatter;
}

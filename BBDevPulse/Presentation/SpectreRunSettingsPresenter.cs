using System.Globalization;

using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Displays the effective settings for a report run without exposing credentials or paths.
/// </summary>
internal static class SpectreRunSettingsPresenter
{
    public static void Render(ReportParameters parameters, string username)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var excludedDates = GetExcludedDatesInWindow(parameters, DateTimeOffset.UtcNow);
        var window = parameters.ToDateExclusive is { } end
            ? $"{parameters.FilterDate:yyyy-MM-dd HH:mm} UTC  →  {end:yyyy-MM-dd HH:mm} UTC (end exclusive)"
            : $"{parameters.FilterDate:yyyy-MM-dd HH:mm} UTC  →  now";

        AnsiConsole.Write(new Rule("[bold deepskyblue1]BBDevPulse  /  Review run[/]").LeftJustified().RuleStyle("grey"));
        AnsiConsole.MarkupLine("[grey]Check the settings below before loading data from Bitbucket.[/]");
        AnsiConsole.WriteLine();

        WriteSection("01  Date range", [
            ("Window", window),
            ("Weekends", parameters.ExcludeWeekend ? "Excluded from duration calculations" : "Included in duration calculations"),
            ("Extra excluded days", parameters.ExcludedDays.Count == 0 ? "None configured" : $"{parameters.ExcludedDays.Count} configured"),
            ("Dates in this window", excludedDates.Count == 0
                ? "None excluded"
                : string.Join(", ", excludedDates.Select(FormatDate)))
        ]);
        AnsiConsole.MarkupLine("[grey]Date exclusions change duration metrics only. PRs and activity on those dates remain eligible.[/]");
        AnsiConsole.WriteLine();

        WriteSection("02  Scope", [
            ("Workspace", parameters.Workspace.Value),
            ("Bitbucket user", username),
            ("Repositories", DescribeRepositorySearch(parameters)),
            ("Merge targets", parameters.BranchNameList.Count == 0
                ? "All target branches"
                : string.Join(", ", parameters.BranchNameList.Select(static branch => branch.Value))),
            ("Team", parameters.TeamFilter ?? "All teams")
        ]);
        AnsiConsole.WriteLine();

        WriteSection("03  Report behavior", [
            ("PR time mode", DescribePrTimeMode(parameters.PrTimeFilterMode)),
            ("PR size", DescribeSizeMode(parameters.PullRequestSizeMode)),
            ("Developer UUIDs", parameters.ShowDeveloperUuidInStats
                ? "Shown beside developers in statistics"
                : "Hidden in developer statistics"),
            ("Developer details", parameters.ShowAllDetailsForDevelopers
                ? "On — include per-developer PR, commit, comment and approval details"
                : "Off — summary statistics only")
        ]);

        if (parameters.ShowAllDetailsForDevelopers)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(
                "[bold orange1]Detailed developer output may substantially increase report generation time.[/]"
            ).Header("[bold orange1]TIME WARNING[/]").BorderColor(Color.Orange1));
        }

        AnsiConsole.WriteLine();
    }

    internal static IReadOnlyList<DateOnly> GetExcludedDatesInWindow(
        ReportParameters parameters, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var first = DateOnly.FromDateTime(parameters.FilterDate.UtcDateTime);
        var lastTimestamp = parameters.ToDateExclusive?.AddTicks(-1) ?? asOf;
        var last = DateOnly.FromDateTime(lastTimestamp.UtcDateTime);
        var excluded = new List<DateOnly>();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            if (parameters.ExcludedDays.Contains(day) ||
                (parameters.ExcludeWeekend && day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                excluded.Add(day);
            }
        }

        return excluded;
    }

    private static void WriteSection(string title, IEnumerable<(string Label, string Value)> rows)
    {
        AnsiConsole.Write(new Rule($"[bold deepskyblue1]{title}[/]").LeftJustified().RuleStyle("grey"));
        var table = new Table().NoBorder().HideHeaders().Expand();
        _ = table.AddColumn(new TableColumn("Setting").Width(22));
        _ = table.AddColumn("Value");
        foreach (var (label, value) in rows)
        {
            _ = table.AddRow($"[grey]{Markup.Escape(label)}[/]", Markup.Escape(value));
        }

        AnsiConsole.Write(table);
    }

    private static string DescribeRepositorySearch(ReportParameters parameters) =>
        parameters.RepoSearchMode switch
        {
            RepoSearchMode.SearchByFilter when string.IsNullOrWhiteSpace(parameters.RepoNameFilter.Value) =>
                "SearchByFilter · all repositories (no name or slug filter)",
            RepoSearchMode.SearchByFilter =>
                $"SearchByFilter · name or slug contains '{parameters.RepoNameFilter.Value}' (case-insensitive)",
            RepoSearchMode.FilterFromTheList when parameters.RepoNameList.Count == 0 =>
                "FilterFromTheList · all repositories (list is empty)",
            RepoSearchMode.FilterFromTheList =>
                $"FilterFromTheList · exact name or slug: {string.Join(", ", parameters.RepoNameList.Select(static name => name.Value))} (case-insensitive)",
            _ => parameters.RepoSearchMode.ToString()
        };

    private static string DescribePrTimeMode(PrTimeFilterMode mode) => mode switch
    {
        PrTimeFilterMode.LastKnownUpdateAndCreated =>
            "LastKnownUpdateAndCreated · created or updated since the start; older PRs with recent activity may appear",
        PrTimeFilterMode.CreatedOnOnly =>
            "CreatedOnOnly · only PRs created since the start; older PRs with recent activity are skipped",
        _ => mode.ToString()
    };

    private static string DescribeSizeMode(PullRequestSizeMode mode) => mode switch
    {
        PullRequestSizeMode.Files => "Files · size statistics and tiers count changed files",
        PullRequestSizeMode.Lines => "Lines · size statistics and tiers count added plus removed lines",
        _ => mode.ToString()
    };

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

using System.Globalization;

using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Displays the effective settings for a report run without exposing credentials or paths.
/// </summary>
internal static class SpectreRunSettingsPresenter
{
    public static void Render(ReportParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var table = new Table().Border(TableBorder.Rounded).Expand();
        _ = table.AddColumn("Setting");
        _ = table.AddColumn("Effective value and impact");

        var end = parameters.ToDateExclusive;
        var window = end.HasValue
            ? $"{parameters.FilterDate:yyyy-MM-dd HH:mm} UTC to {end.Value:yyyy-MM-dd HH:mm} UTC (end exclusive)"
            : $"from {parameters.FilterDate:yyyy-MM-dd HH:mm} UTC to now";
        Add(table, "Date window", window);

        var excludedDates = GetExcludedDatesInWindow(parameters, DateTimeOffset.UtcNow);
        Add(table, "ExcludeWeekend", parameters.ExcludeWeekend ? "true: Saturdays and Sundays" : "false");
        Add(table, "ExcludedDays", parameters.ExcludedDays.Count == 0
            ? "none"
            : string.Join(", ", parameters.ExcludedDays.Order().Select(FormatDate)));
        Add(table, "Excluded in window", excludedDates.Count == 0
            ? "none"
            : string.Join(", ", excludedDates.Select(FormatDate)));
        Add(table, "Date exclusions", "These dates are removed from duration calculations only; PRs and activity on them remain eligible.");

        Add(table, "RepoSearchMode", DescribeRepositorySearch(parameters));
        Add(table, "Merge target branches", parameters.BranchNameList.Count == 0
            ? "all target branches"
            : string.Join(", ", parameters.BranchNameList.Select(static branch => branch.Value)));
        Add(table, "PrTimeFilterMode", parameters.PrTimeFilterMode switch
        {
            PrTimeFilterMode.LastKnownUpdateAndCreated =>
                "LastKnownUpdateAndCreated: include PRs created or last updated since the start; older PRs with recent activity can enter the report.",
            PrTimeFilterMode.CreatedOnOnly =>
                "CreatedOnOnly: fetch PRs created since the start; older PRs with recent activity are not fetched.",
            _ => parameters.PrTimeFilterMode.ToString()
        });
        Add(table, "PullRequestSizeMode", parameters.PullRequestSizeMode switch
        {
            PullRequestSizeMode.Files => "Files: size statistics and tiers use the count of changed files.",
            PullRequestSizeMode.Lines => "Lines: size statistics and tiers use added plus removed lines.",
            _ => parameters.PullRequestSizeMode.ToString()
        });
        Add(table, "ShowDeveloperUuidInStats", parameters.ShowDeveloperUuidInStats
            ? "true: show Bitbucket UUIDs beside developers in statistics."
            : "false: hide Bitbucket UUIDs in developer statistics.");
        Add(table, "ShowAllDetailsForDevelopers", parameters.ShowAllDetailsForDevelopers
            ? "true: include per-developer PR, commit, comment and approval details."
            : "false: show summary statistics without per-developer detail sections.");
        Add(table, "Team filter", parameters.TeamFilter ?? "all teams");

        AnsiConsole.Write(new Rule("Report settings").LeftJustified());
        AnsiConsole.Write(table);
        if (parameters.ShowAllDetailsForDevelopers)
        {
            AnsiConsole.Write(new Panel(
                "[bold red]WARNING: Detailed developer output may substantially increase report generation time.[/]"
            ).BorderColor(Color.Red));
        }
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

    private static string DescribeRepositorySearch(ReportParameters parameters) =>
        parameters.RepoSearchMode switch
        {
            RepoSearchMode.SearchByFilter when string.IsNullOrWhiteSpace(parameters.RepoNameFilter.Value) =>
                "SearchByFilter: no name or slug substring configured; all repositories are eligible.",
            RepoSearchMode.SearchByFilter =>
                $"SearchByFilter: name or slug contains '{parameters.RepoNameFilter.Value}' (case-insensitive).",
            RepoSearchMode.FilterFromTheList when parameters.RepoNameList.Count == 0 =>
                "FilterFromTheList: list is empty; all repositories are eligible.",
            RepoSearchMode.FilterFromTheList =>
                $"FilterFromTheList: name or slug exactly matches one of {string.Join(", ", parameters.RepoNameList.Select(static name => name.Value))} (case-insensitive).",
            _ => parameters.RepoSearchMode.ToString()
        };

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void Add(Table table, string setting, string value) =>
        _ = table.AddRow(Markup.Escape(setting), Markup.Escape(value));
}

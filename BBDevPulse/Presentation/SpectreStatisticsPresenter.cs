using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console statistics presenter.
/// </summary>
public sealed class SpectreStatisticsPresenter : IStatisticsPresenter
{
    private readonly IStatisticsCalculator _statisticsCalculator;
    private readonly IDateDiffFormatter _dateDiffFormatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreStatisticsPresenter"/> class.
    /// </summary>
    /// <param name="statisticsCalculator">Statistics calculator.</param>
    /// <param name="dateDiffFormatter">Date difference formatter.</param>
    public SpectreStatisticsPresenter(
        IStatisticsCalculator statisticsCalculator,
        IDateDiffFormatter dateDiffFormatter)
    {
        ArgumentNullException.ThrowIfNull(statisticsCalculator);
        ArgumentNullException.ThrowIfNull(dateDiffFormatter);
        _statisticsCalculator = statisticsCalculator;
        _dateDiffFormatter = dateDiffFormatter;
    }

    /// <inheritdoc />
    public void RenderMergeTimeStats(ReportData reportData)
    {
        ArgumentNullException.ThrowIfNull(reportData);
        var mergeDays = reportData.Reports
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
    public void RenderTtfrStats(ReportData reportData)
    {
        ArgumentNullException.ThrowIfNull(reportData);
        var ttfrDays = reportData.Reports
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
        ReportData reportData,
        DateTimeOffset filterDate)
    {
        ArgumentNullException.ThrowIfNull(reportData);
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Developer")
            .AddColumn("PRs Opened")
            .AddColumn("PRs Merged")
            .AddColumn("Comments")
            .AddColumn("Approvals");

        var index = 1;
        foreach (var stat in reportData.DeveloperStats.Values
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
}

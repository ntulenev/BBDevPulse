using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Math;
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
        var excludeWeekend = reportData.Parameters.ExcludeWeekend;
        var excludedDays = reportData.Parameters.ExcludedDays;
        var mergeDays = reportData.Reports
            .Where(r => r.MergedOn.HasValue)
            .Select(r => WorkDurationCalculator.Calculate(r.CreatedOn, r.MergedOn!.Value, excludeWeekend, excludedDays).TotalDays)
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
        var excludeWeekend = reportData.Parameters.ExcludeWeekend;
        var excludedDays = reportData.Parameters.ExcludedDays;
        var ttfrDays = reportData.Reports
            .Where(r => r.FirstReactionOn.HasValue)
            .Select(r => WorkDurationCalculator.Calculate(r.CreatedOn, r.FirstReactionOn!.Value, excludeWeekend, excludedDays).TotalDays)
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
    public void RenderCorrectionsStats(ReportData reportData)
    {
        ArgumentNullException.ThrowIfNull(reportData);
        var corrections = reportData.Reports
            .Select(r => (double)r.Corrections)
            .OrderBy(value => value)
            .ToList();

        if (corrections.Count == 0)
        {
            AnsiConsole.Write(new Rule("Corrections Stats").RuleStyle("grey"));
            AnsiConsole.MarkupLine("[yellow]No corrections data available in the report.[/]");
            return;
        }

        var min = corrections.First();
        var max = corrections.Last();
        var median = _statisticsCalculator.Percentile(corrections, 50);
        var p75 = _statisticsCalculator.Percentile(corrections, 75);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Count");

        _ = table.AddRow("Min Corrections", min.ToString("0.##", CultureInfo.InvariantCulture));
        _ = table.AddRow("Max Corrections", max.ToString("0.##", CultureInfo.InvariantCulture));
        _ = table.AddRow("Median", median.ToString("0.##", CultureInfo.InvariantCulture));
        _ = table.AddRow("75P", p75.ToString("0.##", CultureInfo.InvariantCulture));

        AnsiConsole.Write(new Rule("Corrections Stats").RuleStyle("grey"));
        AnsiConsole.Write(table);
    }

    /// <inheritdoc />
    public void RenderWorstPullRequestsTable(ReportData reportData)
    {
        ArgumentNullException.ThrowIfNull(reportData);

        if (reportData.Reports.Count == 0)
        {
            AnsiConsole.Write(new Rule("Worst PRs by Metric").RuleStyle("grey"));
            AnsiConsole.MarkupLine("[yellow]No pull requests available to calculate worst metrics.[/]");
            return;
        }

        var excludeWeekend = reportData.Parameters.ExcludeWeekend;
        var excludedDays = reportData.Parameters.ExcludedDays;

        var mergeCandidates = reportData.Reports
            .Where(static report => report.MergedOn.HasValue)
            .Select(report => new MetricCandidate(
                report,
                WorkDurationCalculator.Calculate(report.CreatedOn, report.MergedOn!.Value, excludeWeekend, excludedDays).TotalDays))
            .OrderByDescending(static candidate => candidate.Value)
            .ToList();

        var ttfrCandidates = reportData.Reports
            .Where(static report => report.FirstReactionOn.HasValue)
            .Select(report => new MetricCandidate(
                report,
                WorkDurationCalculator.Calculate(report.CreatedOn, report.FirstReactionOn!.Value, excludeWeekend, excludedDays).TotalDays))
            .OrderByDescending(static candidate => candidate.Value)
            .ToList();

        var correctionCandidates = reportData.Reports
            .Select(static report => new MetricCandidate(report, report.Corrections))
            .OrderByDescending(static candidate => candidate.Value)
            .ToList();

        var usedPrKeys = new HashSet<string>(StringComparer.Ordinal);
        var longestMerge = SelectDistinctWorst(mergeCandidates, usedPrKeys);
        var longestTtfr = SelectDistinctWorst(ttfrCandidates, usedPrKeys);
        var mostCorrections = SelectDistinctWorst(correctionCandidates, usedPrKeys);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Repository")
            .AddColumn("PR ID")
            .AddColumn("Author")
            .AddColumn("Value");

        AddWorstMetricRow(
            table,
            "Longest Merge Time",
            longestMerge,
            candidate => FormatDurationFromDays(candidate.Value));

        AddWorstMetricRow(
            table,
            "Longest TTFR",
            longestTtfr,
            candidate => FormatDurationFromDays(candidate.Value));

        AddWorstMetricRow(
            table,
            "Most Corrections",
            mostCorrections,
            candidate => candidate.Value.ToString("0.##", CultureInfo.InvariantCulture));

        AnsiConsole.Write(new Rule("Worst PRs by Metric").RuleStyle("grey"));
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
            .AddColumn("Approvals")
            .AddColumn("Corrections");

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
                stat.ApprovalsAfter.ToString(CultureInfo.InvariantCulture),
                stat.Corrections.ToString(CultureInfo.InvariantCulture)
            );
            index++;
        }

        AnsiConsole.Write(new Rule($"Developer Stats (since {filterDate:yyyy-MM-dd})").RuleStyle("grey"));
        AnsiConsole.Write(table);
    }

    private static MetricCandidate? SelectDistinctWorst(
        IEnumerable<MetricCandidate> orderedCandidates,
        HashSet<string> usedPrKeys)
    {
        foreach (var candidate in orderedCandidates)
        {
            var key = BuildPrKey(candidate.Report);
            if (usedPrKeys.Add(key))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void AddWorstMetricRow(
        Table table,
        string metricName,
        MetricCandidate? candidate,
        Func<MetricCandidate, string> formatValue)
    {
        if (candidate is null)
        {
            _ = table.AddRow(metricName, "-", "-", "-", "-");
            return;
        }

        _ = table.AddRow(
            metricName,
            candidate.Value.Report.Repository,
            candidate.Value.Report.Id.Value.ToString(CultureInfo.InvariantCulture),
            candidate.Value.Report.Author,
            formatValue(candidate.Value));
    }

    private string FormatDurationFromDays(double days) =>
        _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(days));

    private static string BuildPrKey(PullRequestReport report) =>
        $"{report.RepositorySlug}:{report.Id.Value.ToString(CultureInfo.InvariantCulture)}";

    private readonly record struct MetricCandidate(PullRequestReport Report, double Value);
}

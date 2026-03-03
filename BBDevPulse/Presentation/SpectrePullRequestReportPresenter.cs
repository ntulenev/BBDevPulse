using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Math;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console pull request report presenter.
/// </summary>
public sealed class SpectrePullRequestReportPresenter : IPullRequestReportPresenter
{
    private const string ActivityOnlyMarkupColor = "orange1";
    private readonly IDateDiffFormatter _dateDiffFormatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectrePullRequestReportPresenter"/> class.
    /// </summary>
    /// <param name="dateDiffFormatter">Date difference formatter.</param>
    public SpectrePullRequestReportPresenter(IDateDiffFormatter dateDiffFormatter)
    {
        ArgumentNullException.ThrowIfNull(dateDiffFormatter);
        _dateDiffFormatter = dateDiffFormatter;
    }

    /// <inheritdoc />
    public void RenderPullRequestTable(
        ReportData reportData,
        DateTimeOffset filterDate)
    {
        ArgumentNullException.ThrowIfNull(reportData);
        var pullRequestSizeMode = reportData.Parameters.PullRequestSizeMode;
        var excludeWeekend = reportData.Parameters.ExcludeWeekend;
        var excludedDays = reportData.Parameters.ExcludedDays;
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
            .AddColumn("Corrections")
            .AddColumn("Size")
            .AddColumn("PR ID");

        var index = 1;
        foreach (var report in reportData.Reports)
        {
            var timeToMerge = report.MergedOn.HasValue
                ? FormatDuration(report.CreatedOn, report.MergedOn.Value, excludeWeekend, excludedDays)
                : "-";
            var prAge = report.State == PullRequestState.Open
                ? WorkDurationCalculator.Calculate(report.CreatedOn, DateTimeOffset.UtcNow, excludeWeekend, excludedDays)
                    .TotalDays
                    .ToString("0.0", CultureInfo.InvariantCulture)
                : "-";
            var ttfr = report.FirstReactionOn.HasValue
                ? FormatDuration(report.CreatedOn, report.FirstReactionOn.Value, excludeWeekend, excludedDays)
                : "-";
            var size = FormatPullRequestSize(report, pullRequestSizeMode);
            var createdCell = report.CreatedOn < filterDate && !report.IsActivityOnlyMatch
                ? $"[red]{report.CreatedOn:yyyy-MM-dd}[/]"
                : FormatCell(report.CreatedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), report.IsActivityOnlyMatch);
            _ = table.AddRow(
                FormatCell(index.ToString(CultureInfo.InvariantCulture), report.IsActivityOnlyMatch),
                FormatCell(report.Repository, report.IsActivityOnlyMatch),
                FormatCell(report.Author, report.IsActivityOnlyMatch),
                FormatCell(report.TargetBranch, report.IsActivityOnlyMatch),
                createdCell,
                FormatCell(ttfr, report.IsActivityOnlyMatch),
                FormatCell(report.LastActivity.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), report.IsActivityOnlyMatch),
                FormatCell(report.MergedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-", report.IsActivityOnlyMatch),
                FormatCell(report.RejectedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-", report.IsActivityOnlyMatch),
                FormatCell(prAge, report.IsActivityOnlyMatch),
                FormatCell(timeToMerge, report.IsActivityOnlyMatch),
                FormatCell(report.Comments.ToString(CultureInfo.InvariantCulture), report.IsActivityOnlyMatch),
                FormatCell(report.Corrections.ToString(CultureInfo.InvariantCulture), report.IsActivityOnlyMatch),
                FormatCell(size, report.IsActivityOnlyMatch),
                FormatCell(report.Id.Value.ToString(CultureInfo.InvariantCulture), report.IsActivityOnlyMatch)
        );

            index++;
        }

        AnsiConsole.Write(new Rule("Pull Requests").RuleStyle("grey"));
        AnsiConsole.Write(table);
        if (reportData.Reports.Any(static report => report.IsActivityOnlyMatch))
        {
            AnsiConsole.MarkupLine($"[{ActivityOnlyMarkupColor}]Orange rows indicate PRs authored outside the selected team but with team activity.[/]");
        }
    }

    private static string FormatCell(string value, bool highlight)
    {
        var escaped = Markup.Escape(value);
        return highlight
            ? $"[{ActivityOnlyMarkupColor}]{escaped}[/]"
            : escaped;
    }

    private static string FormatPullRequestSize(
        PullRequestReport report,
        PullRequestSizeMode sizeMode)
    {
        if (!report.HasSizeDataForMode(sizeMode))
        {
            return "-";
        }

        var sizeValue = report.GetSizeMetricValue(sizeMode);
        var tier = report.GetSizeTier(sizeMode);
        return $"{tier} ({sizeValue.ToString(CultureInfo.InvariantCulture)})";
    }

    private string FormatDuration(
        DateTimeOffset start,
        DateTimeOffset end,
        bool excludeWeekend,
        IReadOnlySet<DateOnly> excludedDays)
    {
        var duration = WorkDurationCalculator.Calculate(start, end, excludeWeekend, excludedDays);
        return _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.Add(duration));
    }
}

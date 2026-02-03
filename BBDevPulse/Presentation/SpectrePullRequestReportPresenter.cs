using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console pull request report presenter.
/// </summary>
public sealed class SpectrePullRequestReportPresenter : IPullRequestReportPresenter
{
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
}

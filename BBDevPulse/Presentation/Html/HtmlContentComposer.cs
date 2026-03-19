using System.Globalization;
using System.Text;

using BBDevPulse.Abstractions;
using BBDevPulse.Math;
using BBDevPulse.Models;
using BBDevPulse.Presentation.Pdf;

namespace BBDevPulse.Presentation.Html;

/// <summary>
/// Composes standalone HTML for the BBDevPulse report.
/// </summary>
public sealed class HtmlContentComposer : IHtmlContentComposer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlContentComposer"/> class.
    /// </summary>
    /// <param name="dateDiffFormatter">Date difference formatter.</param>
    /// <param name="statisticsCalculator">Statistics calculator.</param>
    public HtmlContentComposer(
        IDateDiffFormatter dateDiffFormatter,
        IStatisticsCalculator statisticsCalculator)
    {
        ArgumentNullException.ThrowIfNull(dateDiffFormatter);
        ArgumentNullException.ThrowIfNull(statisticsCalculator);

        _dateDiffFormatter = dateDiffFormatter;
        _statisticsCalculator = statisticsCalculator;
    }

    /// <inheritdoc />
    public string Compose(ReportData reportData)
    {
        ArgumentNullException.ThrowIfNull(reportData);

        var orderedReports = reportData.Reports
            .OrderBy(static report => report.CreatedOn)
            .ToList();
        var metricReports = orderedReports
            .Where(static report => report.IncludeInMetrics)
            .ToList();
        var repositoriesAnalyzed = orderedReports
            .Select(static report => report.Repository)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var pullRequestSizeMode = reportData.Parameters.PullRequestSizeMode;
        var excludeWeekend = reportData.Parameters.ExcludeWeekend;
        var excludedDays = reportData.Parameters.ExcludedDays;
        var workspace = reportData.Parameters.Workspace.Value;

        var content = new StringBuilder(32 * 1024);
        _ = content.Append(BuildPullRequestTable(reportData, orderedReports));
        _ = content.AppendLine("<div class=\"stat-grid\">");
        _ = content.Append(BuildDurationStatsTable(
            "merge-time-stats",
            "Merge Time Stats",
            metricReports
                .Where(static report => report.MergedOn.HasValue)
                .Select(report => WorkDurationCalculator.Calculate(report.CreatedOn, report.MergedOn!.Value, excludeWeekend, excludedDays).TotalDays)
                .OrderBy(static days => days)
                .ToList(),
            "Best Merge Time",
            "Longest Merge Time"));
        _ = content.Append(BuildDurationStatsTable(
            "ttfr-stats",
            "TTFR Stats",
            metricReports
                .Where(static report => report.FirstReactionOn.HasValue)
                .Select(report => WorkDurationCalculator.Calculate(report.CreatedOn, report.FirstReactionOn!.Value, excludeWeekend, excludedDays).TotalDays)
                .OrderBy(static days => days)
                .ToList(),
            "Best TTFR",
            "Longest TTFR"));
        _ = content.Append(BuildCountStatsTable(
            "correction-stats",
            "Corrections Stats",
            metricReports
                .Select(static report => (double)report.Corrections)
                .OrderBy(static value => value)
                .ToList(),
            "Min Corrections",
            "Max Corrections",
            "Count"));
        _ = content.Append(BuildCountStatsTable(
            "pr-size-stats",
            "PR Size Stats",
            metricReports
                .Where(report => report.HasSizeDataForMode(pullRequestSizeMode))
                .Select(report => (double)report.GetSizeMetricValue(pullRequestSizeMode))
                .OrderBy(static value => value)
                .ToList(),
            "Smallest PR",
            "Biggest PR",
            GetPullRequestSizeMetricLabel(pullRequestSizeMode)));
        _ = content.Append(BuildPrThroughputStatsTable(reportData));
        _ = content.Append(BuildPrsPerDeveloperStatsTable(reportData));
        _ = content.Append(BuildCountStatsTable(
            "comments-stats",
            "Comments Stats",
            metricReports
                .Select(static report => (double)report.Comments)
                .OrderBy(static value => value)
                .ToList(),
            "Min Comments",
            "Max Comments",
            "Count"));
        _ = content.AppendLine("</div>");
        _ = content.Append(BuildWorstPullRequestsTable(metricReports, workspace, excludeWeekend, excludedDays, pullRequestSizeMode));
        _ = content.Append(BuildDeveloperStatsTable(reportData));

        if (reportData.Parameters.ShowAllDetailsForDevelopers)
        {
            _ = content.Append(BuildDeveloperDetailsSections(reportData, workspace, pullRequestSizeMode));
        }

        return ApplyTemplate(
            HtmlTemplateLoader.LoadReportTemplate(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["__WORKSPACE__"] = HtmlPresentationHelpers.Encode(workspace),
                ["__GENERATED_AT__"] = HtmlPresentationHelpers.Encode(
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)),
                ["__DATE_WINDOW__"] = HtmlPresentationHelpers.Encode(reportData.Parameters.GetDateWindowLabel()),
                ["__REPOSITORIES_ANALYZED__"] = repositoriesAnalyzed.ToString(CultureInfo.InvariantCulture),
                ["__PULL_REQUEST_COUNT__"] = orderedReports.Count.ToString(CultureInfo.InvariantCulture),
                ["__METRIC_PULL_REQUEST_COUNT__"] = metricReports.Count.ToString(CultureInfo.InvariantCulture),
                ["__DEVELOPER_COUNT__"] = reportData.DeveloperStats.Count.ToString(CultureInfo.InvariantCulture),
                ["__SIZE_MODE__"] = HtmlPresentationHelpers.Encode(GetPullRequestSizeMetricLabel(pullRequestSizeMode)),
                ["__TEAM_FILTER__"] = HtmlPresentationHelpers.Encode(reportData.Parameters.TeamFilter ?? "(all developers)"),
                ["__CONTENT__"] = content.ToString()
            });
    }

    private string BuildPullRequestTable(ReportData reportData, List<PullRequestReport> reports)
    {
        var workspace = reportData.Parameters.Workspace.Value;
        var filterDate = reportData.Parameters.FilterDate;
        var excludeWeekend = reportData.Parameters.ExcludeWeekend;
        var excludedDays = reportData.Parameters.ExcludedDays;
        var pullRequestSizeMode = reportData.Parameters.PullRequestSizeMode;
        var columns = new[]
        {
            new TableColumn("#", "number", "#", "narrow"),
            new TableColumn("Repository", "text", "Repository", "repo-column"),
            new TableColumn("Author", "text", "Author"),
            new TableColumn("Target", "text", "Target branch"),
            new TableColumn("Created", "number", "Created"),
            new TableColumn("TTFR", "number", "TTFR"),
            new TableColumn("Last Activity", "number", "Last activity"),
            new TableColumn("Merged", "number", "Merged"),
            new TableColumn("Rejected", "number", "Rejected"),
            new TableColumn("PR Age", "number", "PR age"),
            new TableColumn("Time to Merge", "number", "Time to merge"),
            new TableColumn("Comments", "number", "Comments"),
            new TableColumn("Corrections", "number", "Corrections"),
            new TableColumn("Size", "number", "Size"),
            new TableColumn("PR ID", "number", "PR ID", "narrow")
        };

        var rows = new List<TableRow>(reports.Count);
        for (var index = 0; index < reports.Count; index++)
        {
            var report = reports[index];
            var repositoryUrl = HtmlPresentationHelpers.BuildRepositoryUrl(workspace, report.RepositorySlug);
            var pullRequestUrl = HtmlPresentationHelpers.BuildPullRequestUrl(workspace, report.RepositorySlug, report.Id.Value);
            var ttfrDuration = report.FirstReactionOn.HasValue
                ? WorkDurationCalculator.Calculate(report.CreatedOn, report.FirstReactionOn.Value, excludeWeekend, excludedDays)
                : (TimeSpan?)null;
            var timeToMergeDuration = report.MergedOn.HasValue
                ? WorkDurationCalculator.Calculate(report.CreatedOn, report.MergedOn.Value, excludeWeekend, excludedDays)
                : (TimeSpan?)null;
            var prAgeDuration = report.State == PullRequestState.Open
                ? WorkDurationCalculator.Calculate(report.CreatedOn, DateTimeOffset.UtcNow, excludeWeekend, excludedDays)
                : (TimeSpan?)null;
            var createdLabel = HtmlPresentationHelpers.FormatDate(report.CreatedOn);
            if (report.CreatedOn < filterDate)
            {
                createdLabel += " *";
            }

            rows.Add(new TableRow(
            [
                BuildTextCell((index + 1).ToString(CultureInfo.InvariantCulture), index + 1),
                BuildLinkCell(report.Repository, repositoryUrl),
                BuildTextCell(report.Author),
                BuildTextCell(report.TargetBranch),
                BuildTextCell(createdLabel, report.CreatedOn.ToUnixTimeSeconds(), HtmlPresentationHelpers.FormatDate(report.CreatedOn)),
                BuildTextCell(ttfrDuration is null ? "-" : FormatDuration(ttfrDuration.Value), ttfrDuration?.TotalMinutes, ttfrDuration is null ? "-" : FormatDuration(ttfrDuration.Value)),
                BuildTextCell(HtmlPresentationHelpers.FormatDate(report.LastActivity), report.LastActivity.ToUnixTimeSeconds()),
                BuildTextCell(HtmlPresentationHelpers.FormatDateTime(report.MergedOn), report.MergedOn?.ToUnixTimeSeconds()),
                BuildTextCell(HtmlPresentationHelpers.FormatDateTime(report.RejectedOn), report.RejectedOn?.ToUnixTimeSeconds()),
                BuildTextCell(prAgeDuration is null ? "-" : FormatDuration(prAgeDuration.Value), prAgeDuration?.TotalMinutes, prAgeDuration is null ? "-" : FormatDuration(prAgeDuration.Value)),
                BuildTextCell(timeToMergeDuration is null ? "-" : FormatDuration(timeToMergeDuration.Value), timeToMergeDuration?.TotalMinutes, timeToMergeDuration is null ? "-" : FormatDuration(timeToMergeDuration.Value)),
                BuildTextCell(report.Comments.ToString(CultureInfo.InvariantCulture), report.Comments),
                BuildTextCell(report.Corrections.ToString(CultureInfo.InvariantCulture), report.Corrections),
                BuildTextCell(FormatPullRequestSize(report, pullRequestSizeMode), report.HasSizeDataForMode(pullRequestSizeMode) ? report.GetSizeMetricValue(pullRequestSizeMode) : null),
                BuildLinkCell(report.Id.Value.ToString(CultureInfo.InvariantCulture), pullRequestUrl, report.Id.Value)
            ], report.IsActivityOnlyMatch ? "activity-only" : null));
        }

        var notes = new StringBuilder();
        if (reports.Any(report => report.CreatedOn < filterDate))
        {
            _ = notes.AppendLine("<p class=\"section-note\">* Created before filter date.</p>");
        }

        if (reports.Any(static report => report.IsActivityOnlyMatch))
        {
            _ = notes.AppendLine("<p class=\"section-note activity-note\">Orange rows indicate PRs authored outside the selected team but with team activity. They do not count in PR metrics and are used only for comment and approval counts in developer stats.</p>");
        }

        return BuildTableSection(
            "pull-requests",
            "Pull Requests",
            reports.Count == 0 ? "No pull requests in the report." : null,
            columns,
            rows,
            defaultSortColumn: 4,
            defaultSortDirection: "asc",
            footerHtml: notes.ToString());
    }

    private string BuildDurationStatsTable(
        string sectionId,
        string title,
        List<double> orderedDays,
        string bestLabel,
        string longestLabel)
    {
        if (orderedDays.Count == 0)
        {
            return BuildTableSection(
                sectionId,
                title,
                "No data available in the report.",
                MetricColumns,
                [],
                defaultSortColumn: 1,
                interactive: false);
        }

        var median = _statisticsCalculator.Percentile(orderedDays, 50);
        var p75 = _statisticsCalculator.Percentile(orderedDays, 75);
        var rows = new List<TableRow>(4)
        {
            BuildMetricRow(bestLabel, FormatDuration(orderedDays[0]), orderedDays[0]),
            BuildMetricRow(longestLabel, FormatDuration(orderedDays[^1]), orderedDays[^1]),
            BuildMetricRow("Median", FormatDuration(median), median),
            BuildMetricRow("75P", FormatDuration(p75), p75)
        };

        return BuildTableSection(
            sectionId,
            title,
            emptyMessage: null,
            MetricColumns,
            rows,
            defaultSortColumn: 1,
            defaultSortDirection: "asc",
            compact: true,
            interactive: false);
    }

    private string BuildCountStatsTable(
        string sectionId,
        string title,
        List<double> orderedValues,
        string minLabel,
        string maxLabel,
        string valueHeader)
    {
        if (orderedValues.Count == 0)
        {
            return BuildTableSection(
                sectionId,
                title,
                "No data available in the report.",
                CreateMetricColumns(valueHeader),
                [],
                defaultSortColumn: 1,
                interactive: false);
        }

        var median = _statisticsCalculator.Percentile(orderedValues, 50);
        var p75 = _statisticsCalculator.Percentile(orderedValues, 75);
        var rows = new List<TableRow>(4)
        {
            BuildMetricRow(minLabel, orderedValues[0].ToString("0.##", CultureInfo.InvariantCulture), orderedValues[0]),
            BuildMetricRow(maxLabel, orderedValues[^1].ToString("0.##", CultureInfo.InvariantCulture), orderedValues[^1]),
            BuildMetricRow("Median", median.ToString("0.##", CultureInfo.InvariantCulture), median),
            BuildMetricRow("75P", p75.ToString("0.##", CultureInfo.InvariantCulture), p75)
        };

        return BuildTableSection(
            sectionId,
            title,
            emptyMessage: null,
            CreateMetricColumns(valueHeader),
            rows,
            defaultSortColumn: 1,
            defaultSortDirection: "asc",
            compact: true,
            interactive: false);
    }

    private static string BuildPrThroughputStatsTable(ReportData reportData)
    {
        var rows = new List<TableRow>(3)
        {
            BuildMetricRow(
                "PRs Created",
                reportData.PullRequestsCreatedInRange.ToString(CultureInfo.InvariantCulture),
                reportData.PullRequestsCreatedInRange),
            BuildMetricRow(
                "PRs Merged",
                reportData.PullRequestsMergedInRange.ToString(CultureInfo.InvariantCulture),
                reportData.PullRequestsMergedInRange),
            BuildMetricRow(
                "PRs Rejected",
                reportData.PullRequestsRejectedInRange.ToString(CultureInfo.InvariantCulture),
                reportData.PullRequestsRejectedInRange)
        };

        return BuildTableSection(
            "pr-throughput-stats",
            "PR Throughput",
            emptyMessage: null,
            CreateMetricColumns("Count"),
            rows,
            defaultSortColumn: 1,
            defaultSortDirection: "desc",
            compact: true,
            interactive: false);
    }

    private string BuildPrsPerDeveloperStatsTable(ReportData reportData)
    {
        var openedPullRequestCounts = reportData.GetOpenedPullRequestCountsPerDeveloper();
        if (openedPullRequestCounts.Count == 0)
        {
            return BuildTableSection(
                "prs-per-developer-stats",
                "PRs per Developer",
                "No authored pull request data available in the report.",
                CreateMetricColumns("Count"),
                [],
                defaultSortColumn: 1,
                interactive: false);
        }

        var median = _statisticsCalculator.Percentile(openedPullRequestCounts, 50);
        var p75 = _statisticsCalculator.Percentile(openedPullRequestCounts, 75);
        var rows = new List<TableRow>(4)
        {
            BuildMetricRow("Min PRs/Developer", openedPullRequestCounts[0].ToString("0.##", CultureInfo.InvariantCulture), openedPullRequestCounts[0]),
            BuildMetricRow("Max PRs/Developer", openedPullRequestCounts[^1].ToString("0.##", CultureInfo.InvariantCulture), openedPullRequestCounts[^1]),
            BuildMetricRow("Median", median.ToString("0.##", CultureInfo.InvariantCulture), median),
            BuildMetricRow("75P", p75.ToString("0.##", CultureInfo.InvariantCulture), p75)
        };

        return BuildTableSection(
            "prs-per-developer-stats",
            "PRs per Developer",
            emptyMessage: null,
            CreateMetricColumns("Count"),
            rows,
            defaultSortColumn: 1,
            defaultSortDirection: "asc",
            compact: true,
            interactive: false);
    }

    private string BuildWorstPullRequestsTable(
        List<PullRequestReport> reports,
        string workspace,
        bool excludeWeekend,
        IReadOnlySet<DateOnly> excludedDays,
        PullRequestSizeMode pullRequestSizeMode)
    {
        var columns = new[]
        {
            new TableColumn("Metric", "text", "Metric"),
            new TableColumn("Repository", "text", "Repository", "repo-column"),
            new TableColumn("PR ID", "number", "PR ID", "narrow"),
            new TableColumn("Author", "text", "Author"),
            new TableColumn("Value", "number", "Value")
        };

        if (reports.Count == 0)
        {
            return BuildTableSection(
                "worst-prs",
                "Worst PRs by Metric",
                "No pull requests available to calculate worst metrics.",
                columns,
                [],
                defaultSortColumn: 4,
                defaultSortDirection: "desc");
        }

        var selections = QuestPdfReportRenderer.BuildWorstMetricSelections(
            reports.ToList(),
            excludeWeekend,
            excludedDays,
            pullRequestSizeMode);
        var rows = new List<TableRow>(selections.Count);

        foreach (var selection in selections)
        {
            if (selection.Report is null || !selection.Value.HasValue)
            {
                rows.Add(new TableRow(
                [
                    BuildTextCell(selection.MetricName),
                    BuildTextCell("-"),
                    BuildTextCell("-"),
                    BuildTextCell("-"),
                    BuildTextCell("-")
                ]));
                continue;
            }

            var report = selection.Report;
            var repositoryUrl = HtmlPresentationHelpers.BuildRepositoryUrl(workspace, report.RepositorySlug);
            var pullRequestUrl = HtmlPresentationHelpers.BuildPullRequestUrl(workspace, report.RepositorySlug, report.Id.Value);
            var valueText = selection.IsDuration
                ? FormatDuration(selection.Value.Value)
                : selection.IsPullRequestSize
                    ? FormatPullRequestSize(selection.Value.Value, pullRequestSizeMode)
                    : selection.Value.Value.ToString("0.##", CultureInfo.InvariantCulture);

            rows.Add(new TableRow(
            [
                BuildTextCell(selection.MetricName),
                BuildLinkCell(report.Repository, repositoryUrl),
                BuildLinkCell(report.Id.Value.ToString(CultureInfo.InvariantCulture), pullRequestUrl, report.Id.Value),
                BuildTextCell(report.Author),
                BuildTextCell(valueText, selection.Value.Value)
            ]));
        }

        return BuildTableSection(
            "worst-prs",
            "Worst PRs by Metric",
            emptyMessage: null,
            columns,
            rows,
            defaultSortColumn: 4,
            defaultSortDirection: "desc");
    }

    private static string BuildDeveloperStatsTable(ReportData reportData)
    {
        var orderedDeveloperStats = reportData.DeveloperStats.Values
            .OrderByDescending(static stat => stat.PrsOpenedSince)
            .ThenBy(static stat => stat.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var showDeveloperUuidInStats = reportData.Parameters.ShowDeveloperUuidInStats;
        var columns = new List<TableColumn>
        {
            new("#", "number", "#", "narrow"),
            new("Developer", "text", "Developer")
        };
        if (showDeveloperUuidInStats)
        {
            columns.Add(new TableColumn("UUID", "text", "UUID"));
        }

        columns.AddRange(
        [
            new TableColumn("Grade", "text", "Grade"),
            new TableColumn("Department", "text", "Department"),
            new TableColumn("PRs Opened", "number", "PRs opened"),
            new TableColumn("PRs Merged", "number", "PRs merged"),
            new TableColumn("Comments", "number", "Comments"),
            new TableColumn("Approvals", "number", "Approvals"),
            new TableColumn("Corrections", "number", "Corrections")
        ]);

        var rows = new List<TableRow>(orderedDeveloperStats.Count);
        for (var index = 0; index < orderedDeveloperStats.Count; index++)
        {
            var stat = orderedDeveloperStats[index];
            var cells = new List<TableCell>
            {
                BuildTextCell((index + 1).ToString(CultureInfo.InvariantCulture), index + 1),
                BuildTextCell(stat.DisplayName.Value)
            };
            if (showDeveloperUuidInStats)
            {
                cells.Add(BuildTextCell(stat.BitbucketUuid?.Value ?? DeveloperStats.NOT_AVAILABLE));
            }

            cells.AddRange(
            [
                BuildTextCell(stat.Grade),
                BuildTextCell(stat.Department),
                BuildTextCell(stat.PrsOpenedSince.ToString(CultureInfo.InvariantCulture), stat.PrsOpenedSince),
                BuildTextCell(stat.PrsMergedAfter.ToString(CultureInfo.InvariantCulture), stat.PrsMergedAfter),
                BuildTextCell(stat.CommentsAfter.ToString(CultureInfo.InvariantCulture), stat.CommentsAfter),
                BuildTextCell(stat.ApprovalsAfter.ToString(CultureInfo.InvariantCulture), stat.ApprovalsAfter),
                BuildTextCell(stat.Corrections.ToString(CultureInfo.InvariantCulture), stat.Corrections)
            ]);

            rows.Add(new TableRow(cells));
        }

        return BuildTableSection(
            "developer-stats",
            $"Developer Stats ({reportData.Parameters.GetDateWindowLabel()})",
            orderedDeveloperStats.Count == 0 ? "No developer activity found in the report." : null,
            columns,
            rows,
            defaultSortColumn: showDeveloperUuidInStats ? 5 : 4,
            defaultSortDirection: "desc");
    }

    private static string BuildDeveloperDetailsSections(
        ReportData reportData,
        string workspace,
        PullRequestSizeMode pullRequestSizeMode)
    {
        var orderedDeveloperStats = reportData.DeveloperStats.Values
            .OrderByDescending(static stat => stat.PrsOpenedSince)
            .ThenBy(static stat => stat.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var html = new StringBuilder();

        _ = html.AppendLine("<section class=\"report-section\">");
        _ = html.AppendLine("  <div class=\"section-header\"><h2>Developer Details</h2></div>");

        if (orderedDeveloperStats.Count == 0)
        {
            _ = html.AppendLine("  <p class=\"empty-section\">No developer activity found in the report.</p>");
            _ = html.AppendLine("</section>");
            return html.ToString();
        }

        for (var index = 0; index < orderedDeveloperStats.Count; index++)
        {
            var stat = orderedDeveloperStats[index];
            var developerId = "developer-" + index.ToString(CultureInfo.InvariantCulture);
            _ = html.AppendLine(string.Concat("  <section class=\"developer-card\" id=\"", developerId, "\">"));
            _ = html.AppendLine(string.Concat("    <h3>", HtmlPresentationHelpers.Encode(stat.DisplayName.Value), "</h3>"));
            _ = html.Append(BuildAuthoredPullRequestsTable(stat, workspace, developerId));
            _ = html.Append(BuildCommentDetailsTable(stat, workspace, developerId));
            _ = html.Append(BuildApprovalDetailsTable(stat, workspace, developerId));
            _ = html.Append(BuildCommitDetailsTable(stat, workspace, developerId, pullRequestSizeMode));
            _ = html.AppendLine("  </section>");
        }

        _ = html.AppendLine("</section>");
        return html.ToString();
    }

    private static string BuildAuthoredPullRequestsTable(
        DeveloperStats stat,
        string workspace,
        string developerId)
    {
        var columns = new[]
        {
            new TableColumn("Repository", "text", "Repository", "repo-column"),
            new TableColumn("PR ID", "number", "PR ID", "narrow"),
            new TableColumn("Target", "text", "Target"),
            new TableColumn("Created", "number", "Created"),
            new TableColumn("State", "text", "State"),
            new TableColumn("Merged", "number", "Merged"),
            new TableColumn("Comments", "number", "Comments"),
            new TableColumn("Fixes", "number", "Fixes"),
            new TableColumn("Size", "number", "Size")
        };
        var rows = stat.AuthoredPullRequests
            .OrderByDescending(static report => report.CreatedOn)
            .Select(report => new TableRow(
            [
                BuildLinkCell(report.Repository, HtmlPresentationHelpers.BuildRepositoryUrl(workspace, report.RepositorySlug)),
                BuildLinkCell(
                    report.Id.Value.ToString(CultureInfo.InvariantCulture),
                    HtmlPresentationHelpers.BuildPullRequestUrl(workspace, report.RepositorySlug, report.Id.Value),
                    report.Id.Value),
                BuildTextCell(report.TargetBranch),
                BuildTextCell(HtmlPresentationHelpers.FormatDate(report.CreatedOn), report.CreatedOn.ToUnixTimeSeconds()),
                BuildTextCell(report.State.ToString()),
                BuildTextCell(HtmlPresentationHelpers.FormatDateTime(report.MergedOn), report.MergedOn?.ToUnixTimeSeconds()),
                BuildTextCell(report.Comments.ToString(CultureInfo.InvariantCulture), report.Comments),
                BuildTextCell(report.Corrections.ToString(CultureInfo.InvariantCulture), report.Corrections),
                BuildTextCell(FormatPullRequestSize(report, PullRequestSizeMode.Lines), report.HasSizeDataForMode(PullRequestSizeMode.Lines) ? report.GetSizeMetricValue(PullRequestSizeMode.Lines) : null)
            ]))
            .ToList();

        return BuildTableSection(
            developerId + "-authored",
            "Authored PRs",
            "No authored PRs.",
            columns,
            rows,
            defaultSortColumn: 3,
            defaultSortDirection: "desc",
            nested: true);
    }

    private static string BuildCommentDetailsTable(DeveloperStats stat, string workspace, string developerId)
    {
        var columns = new[]
        {
            new TableColumn("Repository", "text", "Repository", "repo-column"),
            new TableColumn("PR ID", "number", "PR ID", "narrow"),
            new TableColumn("PR Author", "text", "PR author"),
            new TableColumn("Date", "number", "Date")
        };
        var rows = stat.CommentActivities
            .OrderByDescending(static activity => activity.Date)
            .Select(activity => new TableRow(
            [
                BuildLinkCell(activity.Repository, HtmlPresentationHelpers.BuildRepositoryUrl(workspace, activity.RepositorySlug)),
                BuildLinkCell(
                    activity.PullRequestId.Value.ToString(CultureInfo.InvariantCulture),
                    HtmlPresentationHelpers.BuildPullRequestUrl(workspace, activity.RepositorySlug, activity.PullRequestId.Value),
                    activity.PullRequestId.Value),
                BuildTextCell(activity.PullRequestAuthor),
                BuildTextCell(HtmlPresentationHelpers.FormatDateTime(activity.Date), activity.Date.ToUnixTimeSeconds())
            ]))
            .ToList();

        return BuildTableSection(
            developerId + "-comments",
            "Comments",
            "No comments.",
            columns,
            rows,
            defaultSortColumn: 3,
            defaultSortDirection: "desc",
            nested: true);
    }

    private static string BuildApprovalDetailsTable(DeveloperStats stat, string workspace, string developerId)
    {
        var columns = new[]
        {
            new TableColumn("Repository", "text", "Repository", "repo-column"),
            new TableColumn("PR ID", "number", "PR ID", "narrow"),
            new TableColumn("PR Author", "text", "PR author"),
            new TableColumn("Date", "number", "Date")
        };
        var rows = stat.ApprovalActivities
            .OrderByDescending(static activity => activity.Date)
            .Select(activity => new TableRow(
            [
                BuildLinkCell(activity.Repository, HtmlPresentationHelpers.BuildRepositoryUrl(workspace, activity.RepositorySlug)),
                BuildLinkCell(
                    activity.PullRequestId.Value.ToString(CultureInfo.InvariantCulture),
                    HtmlPresentationHelpers.BuildPullRequestUrl(workspace, activity.RepositorySlug, activity.PullRequestId.Value),
                    activity.PullRequestId.Value),
                BuildTextCell(activity.PullRequestAuthor),
                BuildTextCell(HtmlPresentationHelpers.FormatDateTime(activity.Date), activity.Date.ToUnixTimeSeconds())
            ]))
            .ToList();

        return BuildTableSection(
            developerId + "-approvals",
            "Approvals",
            "No approvals.",
            columns,
            rows,
            defaultSortColumn: 3,
            defaultSortDirection: "desc",
            nested: true);
    }

    private static string BuildCommitDetailsTable(
        DeveloperStats stat,
        string workspace,
        string developerId,
        PullRequestSizeMode pullRequestSizeMode)
    {
        var columns = new[]
        {
            new TableColumn("Repository", "text", "Repository", "repo-column"),
            new TableColumn("PR ID", "number", "PR ID", "narrow"),
            new TableColumn("Commit", "text", "Commit"),
            new TableColumn("Message", "text", "Message"),
            new TableColumn(GetPullRequestSizeMetricLabel(pullRequestSizeMode), "number", GetPullRequestSizeMetricLabel(pullRequestSizeMode)),
            new TableColumn("Date", "number", "Date")
        };
        var rows = stat.CommitActivities
            .OrderByDescending(static activity => activity.Date)
            .Select(activity => new TableRow(
            [
                BuildLinkCell(activity.Repository, HtmlPresentationHelpers.BuildRepositoryUrl(workspace, activity.RepositorySlug)),
                BuildLinkCell(
                    activity.PullRequestId.Value.ToString(CultureInfo.InvariantCulture),
                    HtmlPresentationHelpers.BuildPullRequestUrl(workspace, activity.RepositorySlug, activity.PullRequestId.Value),
                    activity.PullRequestId.Value),
                BuildLinkCell(
                    HtmlPresentationHelpers.ShortCommitHash(activity.CommitHash),
                    HtmlPresentationHelpers.BuildCommitUrl(workspace, activity.RepositorySlug, activity.CommitHash)),
                BuildTextCell(HtmlPresentationHelpers.TrimCommitMessage(activity.Message)),
                BuildTextCell(
                    FormatCommitActivitySize(activity, pullRequestSizeMode),
                    activity.HasSizeDataForMode(pullRequestSizeMode) ? activity.GetSizeMetricValue(pullRequestSizeMode) : null),
                BuildTextCell(HtmlPresentationHelpers.FormatDateTime(activity.Date), activity.Date.ToUnixTimeSeconds())
            ]))
            .ToList();

        return BuildTableSection(
            developerId + "-commits",
            "Follow-up Commits",
            "No follow-up commits.",
            columns,
            rows,
            defaultSortColumn: 5,
            defaultSortDirection: "desc",
            nested: true);
    }

    private static string BuildTableSection(
        string sectionId,
        string title,
        string? emptyMessage,
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<TableRow> rows,
        int? defaultSortColumn,
        string defaultSortDirection = "asc",
        string? footerHtml = null,
        bool compact = false,
        bool nested = false,
        bool interactive = true)
    {
        var html = new StringBuilder();
        var containerClass = nested ? "table-section nested-section" : compact ? "table-section compact-section" : "table-section";

        _ = html.AppendLine(string.Concat("<section class=\"", containerClass, "\" id=\"", HtmlPresentationHelpers.EncodeAttribute(sectionId), "\">"));
        _ = html.AppendLine("  <div class=\"section-header\">");
        _ = html.AppendLine(string.Concat("    <h2>", HtmlPresentationHelpers.Encode(title), "</h2>"));
        _ = html.AppendLine("  </div>");
        _ = html.AppendLine(interactive
            ? "  <div class=\"table-panel\" data-table-panel>"
            : "  <div class=\"table-panel\">");
        if (interactive)
        {
            _ = html.AppendLine("    <div class=\"table-controls\">");
            _ = html.AppendLine("      <input class=\"search\" data-table-search type=\"search\" placeholder=\"Search this table\">");
            _ = html.AppendLine("      <button class=\"button\" data-table-reset type=\"button\">Reset Filters</button>");
            _ = html.AppendLine("    </div>");
        }

        _ = html.AppendLine("    <div class=\"table-wrap\">");
        _ = html.AppendLine("      <div class=\"scroll\">");
        _ = html.AppendLine(string.Concat(
            "        <table class=\"report-table\" data-default-sort-column=\"",
            defaultSortColumn.HasValue ? defaultSortColumn.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
            "\" data-default-sort-direction=\"",
            HtmlPresentationHelpers.EncodeAttribute(defaultSortDirection),
            "\">"));
        _ = html.AppendLine("          <thead>");
        _ = html.AppendLine("            <tr>");

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var column = columns[columnIndex];
            var thClass = string.IsNullOrWhiteSpace(column.CssClass) ? string.Empty : $" class=\"{HtmlPresentationHelpers.EncodeAttribute(column.CssClass)}\"";
            if (interactive)
            {
                _ = html.AppendLine(string.Concat(
                    "              <th",
                    thClass,
                    "><button class=\"th-button\" data-sort-column=\"",
                    columnIndex.ToString(CultureInfo.InvariantCulture),
                    "\" data-sort-type=\"",
                    HtmlPresentationHelpers.EncodeAttribute(column.SortType),
                    "\" type=\"button\"><span>",
                    HtmlPresentationHelpers.Encode(column.Header),
                    "</span><span class=\"sort-indicator\"></span></button></th>"));
            }
            else
            {
                _ = html.AppendLine(string.Concat(
                    "              <th",
                    thClass,
                    ">",
                    HtmlPresentationHelpers.Encode(column.Header),
                    "</th>"));
            }
        }

        _ = html.AppendLine("            </tr>");
        if (interactive)
        {
            _ = html.AppendLine("            <tr class=\"filters\">");

            foreach (var column in columns)
            {
                var thClass = string.IsNullOrWhiteSpace(column.CssClass) ? string.Empty : $" class=\"{HtmlPresentationHelpers.EncodeAttribute(column.CssClass)}\"";
                _ = html.AppendLine(string.Concat(
                    "              <th",
                    thClass,
                    "><input class=\"filter-input\" data-filter-column placeholder=\"",
                    HtmlPresentationHelpers.EncodeAttribute(column.FilterPlaceholder),
                    "\" type=\"search\"></th>"));
            }

            _ = html.AppendLine("            </tr>");
        }
        _ = html.AppendLine("          </thead>");
        _ = html.AppendLine("          <tbody>");

        if (rows.Count == 0)
        {
            _ = html.AppendLine(string.Concat(
                "            <tr class=\"empty\"><td class=\"empty-cell\" colspan=\"",
                columns.Count.ToString(CultureInfo.InvariantCulture),
                "\">",
                HtmlPresentationHelpers.Encode(emptyMessage ?? "No data available."),
                "</td></tr>"));
        }
        else
        {
            foreach (var row in rows)
            {
                var rowClass = string.IsNullOrWhiteSpace(row.CssClass) ? string.Empty : $" class=\"{HtmlPresentationHelpers.EncodeAttribute(row.CssClass)}\"";
                _ = html.AppendLine(string.Concat("            <tr", rowClass, ">"));

                foreach (var cell in row.Cells)
                {
                    var cellClass = string.IsNullOrWhiteSpace(cell.CssClass) ? string.Empty : $" class=\"{HtmlPresentationHelpers.EncodeAttribute(cell.CssClass)}\"";
                    _ = html.AppendLine(string.Concat(
                        "              <td",
                        cellClass,
                        " data-sort='",
                        HtmlPresentationHelpers.EncodeAttribute(cell.SortValue),
                        "' data-filter='",
                        HtmlPresentationHelpers.EncodeAttribute(cell.FilterValue),
                        "'>",
                        cell.Html,
                        "</td>"));
                }

                _ = html.AppendLine("            </tr>");
            }
        }

        _ = html.AppendLine("          </tbody>");
        _ = html.AppendLine("        </table>");
        _ = html.AppendLine("      </div>");
        _ = html.AppendLine("    </div>");

        if (!string.IsNullOrWhiteSpace(footerHtml))
        {
            _ = html.AppendLine(footerHtml);
        }

        _ = html.AppendLine("  </div>");
        _ = html.AppendLine("</section>");
        return html.ToString();
    }

    private static string ApplyTemplate(string template, IReadOnlyDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(tokens);

        var result = template;
        foreach (var token in tokens)
        {
            result = result.Replace(token.Key, token.Value, StringComparison.Ordinal);
        }

        return result;
    }

    private static TableRow BuildMetricRow(string metricName, string metricValue, double sortValue) =>
        new(
        [
            BuildTextCell(metricName),
            BuildTextCell(metricValue, sortValue)
        ]);

    private static TableCell BuildTextCell(string text, IFormattable? sortValue = null, string? filterValue = null, string? cssClass = null) =>
        new(
            HtmlPresentationHelpers.Encode(text),
            sortValue is null
                ? HtmlPresentationHelpers.EncodeAttribute(text)
                : sortValue.ToString(null, CultureInfo.InvariantCulture),
            filterValue ?? text,
            cssClass);

    private static TableCell BuildLinkCell(string text, string url, long? sortValue = null, string? cssClass = null)
    {
        var encodedUrl = HtmlPresentationHelpers.EncodeAttribute(url);
        var encodedText = HtmlPresentationHelpers.Encode(text);
        return new TableCell(
            $"<a href=\"{encodedUrl}\" target=\"_blank\" rel=\"noreferrer\">{encodedText}</a>",
            sortValue?.ToString(CultureInfo.InvariantCulture) ?? text,
            text,
            cssClass);
    }

    private string FormatDuration(double totalDays) =>
        _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(totalDays));

    private string FormatDuration(TimeSpan duration) =>
        _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.Add(duration));

    private static string GetPullRequestSizeMetricLabel(PullRequestSizeMode mode)
    {
        return mode switch
        {
            PullRequestSizeMode.Lines => "Churn",
            PullRequestSizeMode.Files => "Files",
            _ => "Churn"
        };
    }

    private static string FormatPullRequestSize(PullRequestReport report, PullRequestSizeMode sizeMode)
    {
        if (!report.HasSizeDataForMode(sizeMode))
        {
            return "-";
        }

        var sizeValue = report.GetSizeMetricValue(sizeMode);
        var tier = report.GetSizeTier(sizeMode);
        return $"{tier} ({sizeValue.ToString(CultureInfo.InvariantCulture)})";
    }

    private static string FormatPullRequestSize(double value, PullRequestSizeMode sizeMode)
    {
        var rounded = (int)System.Math.Round(value, MidpointRounding.AwayFromZero);
        return $"{rounded.ToString(CultureInfo.InvariantCulture)} ({PullRequestSizeClassifier.Classify(rounded, sizeMode)})";
    }

    private static string FormatCommitActivitySize(
        DeveloperCommitActivity activity,
        PullRequestSizeMode pullRequestSizeMode) =>
        activity.HasSizeDataForMode(pullRequestSizeMode)
            ? activity.GetSizeMetricValue(pullRequestSizeMode).ToString(CultureInfo.InvariantCulture)
            : "-";

    private static IReadOnlyList<TableColumn> MetricColumns { get; } = CreateMetricColumns("Value");

    private static IReadOnlyList<TableColumn> CreateMetricColumns(string valueHeader) =>
    [
        new TableColumn("Metric", "text", "Metric"),
        new TableColumn(valueHeader, "number", valueHeader)
    ];

    private sealed record TableColumn(string Header, string SortType, string FilterPlaceholder, string? CssClass = null);

    private sealed record TableCell(string Html, string SortValue, string FilterValue, string? CssClass = null);

    private sealed record TableRow(IReadOnlyList<TableCell> Cells, string? CssClass = null);

    private readonly IDateDiffFormatter _dateDiffFormatter;
    private readonly IStatisticsCalculator _statisticsCalculator;
}

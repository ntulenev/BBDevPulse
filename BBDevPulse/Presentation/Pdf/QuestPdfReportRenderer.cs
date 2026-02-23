using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Math;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using QLicenseType = QuestPDF.Infrastructure.LicenseType;

namespace BBDevPulse.Presentation.Pdf;

/// <summary>
/// QuestPDF implementation for BBDevPulse report rendering.
/// </summary>
internal sealed class QuestPdfReportRenderer : IPdfReportRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestPdfReportRenderer"/> class.
    /// </summary>
    /// <param name="options">Bitbucket options.</param>
    /// <param name="dateDiffFormatter">Date difference formatter.</param>
    /// <param name="statisticsCalculator">Statistics calculator.</param>
    /// <param name="pdfReportFileStore">PDF output file store.</param>
    public QuestPdfReportRenderer(
        IOptions<BitbucketOptions> options,
        IDateDiffFormatter dateDiffFormatter,
        IStatisticsCalculator statisticsCalculator,
        IPdfReportFileStore pdfReportFileStore)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dateDiffFormatter);
        ArgumentNullException.ThrowIfNull(statisticsCalculator);
        ArgumentNullException.ThrowIfNull(pdfReportFileStore);

        _dateDiffFormatter = dateDiffFormatter;
        _statisticsCalculator = statisticsCalculator;
        _pdfReportFileStore = pdfReportFileStore;
        _pdfOptions = options.Value.Pdf ?? new PdfOptions();
    }

    /// <inheritdoc />
    public void RenderReport(ReportData reportData)
    {
        ArgumentNullException.ThrowIfNull(reportData);

        if (!_pdfOptions.Enabled)
        {
            return;
        }

        var orderedReports = reportData.Reports
            .OrderBy(static report => report.CreatedOn)
            .ToList();
        var outputPath = _pdfOptions.ResolveOutputPath();
        var filterDate = reportData.Parameters.FilterDate;
        var excludeWeekend = reportData.Parameters.ExcludeWeekend;
        var excludedDays = reportData.Parameters.ExcludedDays;
        var workspace = reportData.Parameters.Workspace.Value;

        QuestPDF.Settings.License = QLicenseType.Community;

        var document = Document.Create(container =>
        {
            _ = container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(16);
                page.DefaultTextStyle(static style => style.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Spacing(2);
                    _ = column.Item().Text("BBDevPulse").Bold().FontSize(16);
                    _ = column.Item().Text(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Generated: {0:yyyy-MM-dd HH:mm:ss zzz}",
                            DateTimeOffset.Now));
                    _ = column.Item().Text("Workspace: " + reportData.Parameters.Workspace.Value);
                    _ = column.Item().Text(
                        "Filter date: "
                        + filterDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        + "    Repositories analyzed: "
                        + orderedReports.Select(static report => report.Repository).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(CultureInfo.InvariantCulture));
                });

                page.Content().PaddingTop(8).Column(column =>
                {
                    column.Spacing(8);
                    ComposePullRequestSection(column, orderedReports, filterDate, workspace, excludeWeekend, excludedDays);
                    ComposeDurationSection(
                        column,
                        "Merge Time Stats",
                        orderedReports
                            .Where(static report => report.MergedOn.HasValue)
                            .Select(report => WorkDurationCalculator.Calculate(report.CreatedOn, report.MergedOn!.Value, excludeWeekend, excludedDays).TotalDays)
                            .OrderBy(static days => days)
                            .ToList(),
                        "Best Merge Time",
                        "Longest Merge Time");
                    ComposeDurationSection(
                        column,
                        "TTFR Stats",
                        orderedReports
                            .Where(static report => report.FirstReactionOn.HasValue)
                            .Select(report => WorkDurationCalculator.Calculate(report.CreatedOn, report.FirstReactionOn!.Value, excludeWeekend, excludedDays).TotalDays)
                            .OrderBy(static days => days)
                            .ToList(),
                        "Best TTFR",
                        "Longest TTFR");
                    ComposeCountSection(
                        column,
                        "Corrections Stats",
                        orderedReports
                            .Select(static report => (double)report.Corrections)
                            .OrderBy(static value => value)
                            .ToList(),
                        "Min Corrections",
                        "Max Corrections");
                    ComposeCountSection(
                        column,
                        "PR Size Stats",
                        orderedReports
                            .Where(static report => report.HasSizeData)
                            .Select(static report => (double)report.LineChurn)
                            .OrderBy(static value => value)
                            .ToList(),
                        "Smallest PR",
                        "Biggest PR");
                    ComposeWorstPullRequestsSection(column, orderedReports, workspace, excludeWeekend, excludedDays);
                    ComposeDeveloperSection(column, reportData);
                });

                page.Footer().AlignRight().Text(text =>
                {
                    _ = text.Span("Page ");
                    _ = text.CurrentPageNumber();
                    _ = text.Span(" / ");
                    _ = text.TotalPages();
                });
            });
        });

        _pdfReportFileStore.Save(outputPath, document);

        Console.WriteLine($"PDF report saved to: {outputPath}");
    }

    private void ComposePullRequestSection(
        ColumnDescriptor column,
        List<PullRequestReport> reports,
        DateTimeOffset filterDate,
        string workspace,
        bool excludeWeekend,
        IReadOnlySet<DateOnly> excludedDays)
    {
        _ = column.Item().Text("Pull Requests").Bold().FontSize(12);
        if (reports.Count == 0)
        {
            _ = column.Item().Text("No pull requests in the report.");
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(20);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1.2f);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.ConstantColumn(50);
                columns.ConstantColumn(58);
                columns.ConstantColumn(45);
                columns.ConstantColumn(50);
                columns.ConstantColumn(58);
                columns.ConstantColumn(35);
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(HeaderCell).Text("#");
                _ = header.Cell().Element(HeaderCell).Text("Repository");
                _ = header.Cell().Element(HeaderCell).Text("Author");
                _ = header.Cell().Element(HeaderCell).Text("Target");
                _ = header.Cell().Element(HeaderCell).Text("Created");
                _ = header.Cell().Element(HeaderCell).Text("TTFR");
                _ = header.Cell().Element(HeaderCell).Text("Last Activity");
                _ = header.Cell().Element(HeaderCell).Text("Merged");
                _ = header.Cell().Element(HeaderCell).Text("Rejected");
                _ = header.Cell().Element(HeaderCell).Text("PR Age");
                _ = header.Cell().Element(HeaderCell).Text("Time to Merge");
                _ = header.Cell().Element(HeaderCell).Text("Comments");
                _ = header.Cell().Element(HeaderCell).Text("Corrections");
                _ = header.Cell().Element(HeaderCell).Text("Size");
                _ = header.Cell().Element(HeaderCell).Text("PR ID");
            });

            var index = 1;
            foreach (var report in reports)
            {
                var repositoryUrl = BuildRepositoryUrl(workspace, report.RepositorySlug);
                var pullRequestUrl = BuildPullRequestUrl(workspace, report.RepositorySlug, report.Id.Value);
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
                var size = report.HasSizeData
                    ? $"{report.SizeTier} ({report.LineChurn.ToString(CultureInfo.InvariantCulture)})"
                    : "-";
                var created = report.CreatedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (report.CreatedOn < filterDate)
                {
                    created += " *";
                }

                _ = table.Cell().Element(BodyCell).Text(index.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Hyperlink(repositoryUrl).Text(text =>
                {
                    text.Span(report.Repository).FontColor(Colors.Blue.Medium).Underline();
                });
                _ = table.Cell().Element(BodyCell).Text(report.Author);
                _ = table.Cell().Element(BodyCell).Text(report.TargetBranch);
                _ = table.Cell().Element(BodyCell).Text(created);
                _ = table.Cell().Element(BodyCell).Text(ttfr);
                _ = table.Cell().Element(BodyCell).Text(report.LastActivity.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                _ = table.Cell().Element(BodyCell).Text(report.MergedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-");
                _ = table.Cell().Element(BodyCell).Text(report.RejectedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-");
                _ = table.Cell().Element(BodyCell).Text(prAge);
                _ = table.Cell().Element(BodyCell).Text(timeToMerge);
                _ = table.Cell().Element(BodyCell).Text(report.Comments.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(BodyCell).Text(report.Corrections.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(BodyCell).Text(size);
                table.Cell().Element(BodyCell).Hyperlink(pullRequestUrl).Text(text =>
                {
                    text.Span(report.Id.Value.ToString(CultureInfo.InvariantCulture)).FontColor(Colors.Blue.Medium).Underline();
                });
                index++;
            }
        });

        if (reports.Any(report => report.CreatedOn < filterDate))
        {
            _ = column.Item().PaddingTop(2).Text("* Created before filter date").Italic().FontSize(8);
        }
    }

    private void ComposeDurationSection(
        ColumnDescriptor column,
        string title,
        List<double> orderedDays,
        string bestLabel,
        string longestLabel)
    {
        _ = column.Item().Text(title).Bold().FontSize(12);
        if (orderedDays.Count == 0)
        {
            _ = column.Item().Text("No data available in the report.");
            return;
        }

        var best = orderedDays[0];
        var longest = orderedDays[^1];
        var median = _statisticsCalculator.Percentile(orderedDays, 50);
        var p75 = _statisticsCalculator.Percentile(orderedDays, 75);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(HeaderCell).Text("Metric");
                _ = header.Cell().Element(HeaderCell).Text("Time");
            });

            AddMetricRow(table, bestLabel, FormatDuration(best));
            AddMetricRow(table, longestLabel, FormatDuration(longest));
            AddMetricRow(table, "Median", FormatDuration(median));
            AddMetricRow(table, "75P", FormatDuration(p75));
        });
    }

    private static void ComposeDeveloperSection(ColumnDescriptor column, ReportData reportData)
    {
        _ = column.Item().Text(
            string.Format(
                CultureInfo.InvariantCulture,
                "Developer Stats (since {0:yyyy-MM-dd})",
                reportData.Parameters.FilterDate)).Bold().FontSize(12);

        var orderedDeveloperStats = reportData.DeveloperStats.Values
            .OrderByDescending(static stat => stat.PrsOpenedSince)
            .ThenBy(static stat => stat.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (orderedDeveloperStats.Count == 0)
        {
            _ = column.Item().Text("No developer activity found in the report.");
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(20);
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1.2f);
                columns.ConstantColumn(45);
                columns.ConstantColumn(45);
                columns.ConstantColumn(45);
                columns.ConstantColumn(45);
                columns.ConstantColumn(45);
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(HeaderCell).Text("#");
                _ = header.Cell().Element(HeaderCell).Text("Developer");
                _ = header.Cell().Element(HeaderCell).Text("Grade");
                _ = header.Cell().Element(HeaderCell).Text("Department");
                _ = header.Cell().Element(HeaderCell).Text("PRs Opened");
                _ = header.Cell().Element(HeaderCell).Text("PRs Merged");
                _ = header.Cell().Element(HeaderCell).Text("Comments");
                _ = header.Cell().Element(HeaderCell).Text("Approvals");
                _ = header.Cell().Element(HeaderCell).Text("Corrections");
            });

            var index = 1;
            foreach (var stat in orderedDeveloperStats)
            {
                _ = table.Cell().Element(BodyCell).Text(index.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(BodyCell).Text(stat.DisplayName.Value);
                _ = table.Cell().Element(BodyCell).Text(stat.Grade);
                _ = table.Cell().Element(BodyCell).Text(stat.Department);
                _ = table.Cell().Element(BodyCell).Text(stat.PrsOpenedSince.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(BodyCell).Text(stat.PrsMergedAfter.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(BodyCell).Text(stat.CommentsAfter.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(BodyCell).Text(stat.ApprovalsAfter.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(BodyCell).Text(stat.Corrections.ToString(CultureInfo.InvariantCulture));
                index++;
            }
        });
    }

    private void ComposeWorstPullRequestsSection(
        ColumnDescriptor column,
        List<PullRequestReport> reports,
        string workspace,
        bool excludeWeekend,
        IReadOnlySet<DateOnly> excludedDays)
    {
        _ = column.Item().Text("Worst PRs by Metric").Bold().FontSize(12);
        if (reports.Count == 0)
        {
            _ = column.Item().Text("No pull requests available to calculate worst metrics.");
            return;
        }

        var selections = BuildWorstMetricSelections(reports, excludeWeekend, excludedDays);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(130);
                columns.RelativeColumn(2);
                columns.ConstantColumn(45);
                columns.RelativeColumn(1.5f);
                columns.ConstantColumn(90);
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(HeaderCell).Text("Metric");
                _ = header.Cell().Element(HeaderCell).Text("Repository");
                _ = header.Cell().Element(HeaderCell).Text("PR ID");
                _ = header.Cell().Element(HeaderCell).Text("Author");
                _ = header.Cell().Element(HeaderCell).Text("Value");
            });

            foreach (var selection in selections)
            {
                _ = table.Cell().Element(BodyCell).Text(selection.MetricName);

                if (selection.Report is null || !selection.Value.HasValue)
                {
                    _ = table.Cell().Element(BodyCell).Text("-");
                    _ = table.Cell().Element(BodyCell).Text("-");
                    _ = table.Cell().Element(BodyCell).Text("-");
                    _ = table.Cell().Element(BodyCell).Text("-");
                    continue;
                }

                var report = selection.Report;
                var repositoryUrl = BuildRepositoryUrl(workspace, report.RepositorySlug);
                var pullRequestUrl = BuildPullRequestUrl(workspace, report.RepositorySlug, report.Id.Value);
                table.Cell().Element(BodyCell).Hyperlink(repositoryUrl).Text(text =>
                {
                    text.Span(report.Repository).FontColor(Colors.Blue.Medium).Underline();
                });
                table.Cell().Element(BodyCell).Hyperlink(pullRequestUrl).Text(text =>
                {
                    text.Span(report.Id.Value.ToString(CultureInfo.InvariantCulture)).FontColor(Colors.Blue.Medium).Underline();
                });
                _ = table.Cell().Element(BodyCell).Text(report.Author);
                _ = table.Cell().Element(BodyCell).Text(
                    selection.IsDuration
                        ? FormatDuration(selection.Value.Value)
                        : selection.IsPullRequestSize
                            ? FormatPullRequestSize(selection.Value.Value)
                            : selection.Value.Value.ToString("0.##", CultureInfo.InvariantCulture));
            }
        });
    }

    private void ComposeCountSection(
        ColumnDescriptor column,
        string title,
        List<double> orderedValues,
        string minLabel,
        string maxLabel)
    {
        _ = column.Item().Text(title).Bold().FontSize(12);
        if (orderedValues.Count == 0)
        {
            _ = column.Item().Text("No data available in the report.");
            return;
        }

        var min = orderedValues[0];
        var max = orderedValues[^1];
        var median = _statisticsCalculator.Percentile(orderedValues, 50);
        var p75 = _statisticsCalculator.Percentile(orderedValues, 75);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(HeaderCell).Text("Metric");
                _ = header.Cell().Element(HeaderCell).Text("Count");
            });

            AddMetricRow(table, minLabel, min.ToString("0.##", CultureInfo.InvariantCulture));
            AddMetricRow(table, maxLabel, max.ToString("0.##", CultureInfo.InvariantCulture));
            AddMetricRow(table, "Median", median.ToString("0.##", CultureInfo.InvariantCulture));
            AddMetricRow(table, "75P", p75.ToString("0.##", CultureInfo.InvariantCulture));
        });
    }

    private static void AddMetricRow(TableDescriptor table, string metricName, string metricValue)
    {
        _ = table.Cell().Element(BodyCell).Text(metricName);
        _ = table.Cell().Element(BodyCell).Text(metricValue);
    }

    internal static IReadOnlyList<WorstMetricSelection> BuildWorstMetricSelections(
        List<PullRequestReport> reports,
        bool excludeWeekend,
        IReadOnlySet<DateOnly> excludedDays)
    {
        var mergeCandidates = reports
            .Where(static report => report.MergedOn.HasValue)
            .Select(report => new MetricCandidate(
                report,
                WorkDurationCalculator.Calculate(report.CreatedOn, report.MergedOn!.Value, excludeWeekend, excludedDays).TotalDays))
            .OrderByDescending(static candidate => candidate.Value)
            .ToList();

        var ttfrCandidates = reports
            .Where(static report => report.FirstReactionOn.HasValue)
            .Select(report => new MetricCandidate(
                report,
                WorkDurationCalculator.Calculate(report.CreatedOn, report.FirstReactionOn!.Value, excludeWeekend, excludedDays).TotalDays))
            .OrderByDescending(static candidate => candidate.Value)
            .ToList();

        var correctionCandidates = reports
            .Select(static report => new MetricCandidate(report, report.Corrections))
            .OrderByDescending(static candidate => candidate.Value)
            .ToList();

        var sizeCandidates = reports
            .Where(static report => report.HasSizeData)
            .Select(static report => new MetricCandidate(report, report.LineChurn))
            .OrderByDescending(static candidate => candidate.Value)
            .ToList();

        var usedPrKeys = new HashSet<string>(StringComparer.Ordinal);
        var longestMerge = SelectDistinctWorst(mergeCandidates, usedPrKeys);
        var longestTtfr = SelectDistinctWorst(ttfrCandidates, usedPrKeys);
        var mostCorrections = SelectDistinctWorst(correctionCandidates, usedPrKeys);
        var biggestPr = SelectDistinctWorst(sizeCandidates, usedPrKeys);

        return
        [
            CreateSelection("Longest Merge Time", longestMerge, isDuration: true, isPullRequestSize: false),
            CreateSelection("Longest TTFR", longestTtfr, isDuration: true, isPullRequestSize: false),
            CreateSelection("Most Corrections", mostCorrections, isDuration: false, isPullRequestSize: false),
            CreateSelection("Biggest PR", biggestPr, isDuration: false, isPullRequestSize: true)
        ];
    }

    private string FormatDuration(double totalDays) =>
        _dateDiffFormatter.Format(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddDays(totalDays));

    private static string FormatPullRequestSize(double lineChurn)
    {
        var rounded = (int)System.Math.Round(lineChurn, MidpointRounding.AwayFromZero);
        return $"{rounded.ToString(CultureInfo.InvariantCulture)} ({PullRequestSizeClassifier.Classify(rounded)})";
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

    private static WorstMetricSelection CreateSelection(
        string metricName,
        MetricCandidate? candidate,
        bool isDuration,
        bool isPullRequestSize)
    {
        return candidate is null
            ? new WorstMetricSelection(metricName, null, null, isDuration, isPullRequestSize)
            : new WorstMetricSelection(metricName, candidate.Value.Report, candidate.Value.Value, isDuration, isPullRequestSize);
    }

    private static string BuildPrKey(PullRequestReport report) =>
        $"{report.RepositorySlug}:{report.Id.Value.ToString(CultureInfo.InvariantCulture)}";

    private static string BuildRepositoryUrl(string workspace, string repositorySlug) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "https://bitbucket.org/{0}/{1}/",
            Uri.EscapeDataString(workspace),
            Uri.EscapeDataString(repositorySlug));

    private static string BuildPullRequestUrl(string workspace, string repositorySlug, int pullRequestId) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "https://bitbucket.org/{0}/{1}/pull-requests/{2}",
            Uri.EscapeDataString(workspace),
            Uri.EscapeDataString(repositorySlug),
            pullRequestId.ToString(CultureInfo.InvariantCulture));

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(2)
            .PaddingHorizontal(3);

    private static IContainer BodyCell(IContainer container) =>
        container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(2)
            .PaddingHorizontal(3);

    private readonly IDateDiffFormatter _dateDiffFormatter;
    private readonly IStatisticsCalculator _statisticsCalculator;
    private readonly IPdfReportFileStore _pdfReportFileStore;
    private readonly PdfOptions _pdfOptions;

    internal readonly record struct WorstMetricSelection(
        string MetricName,
        PullRequestReport? Report,
        double? Value,
        bool IsDuration,
        bool IsPullRequestSize);

    private readonly record struct MetricCandidate(PullRequestReport Report, double Value);
}

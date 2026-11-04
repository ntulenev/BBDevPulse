using System.Net;
using System.Text.RegularExpressions;

using BBDevPulse.Configuration;
using BBDevPulse.Math;
using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Presentation.Formatters;
using BBDevPulse.Presentation.Html;
using BBDevPulse.Presentation.Pdf;
using BBDevPulse.Tests.TestInfrastructure;

using Microsoft.Extensions.Options;

using UglyToad.PdfPig;

namespace BBDevPulse.Tests.Presentation;

public sealed partial class ReportFormatParityTests
{
    [Theory]
    [InlineData(PullRequestSizeMode.Lines)]
    [InlineData(PullRequestSizeMode.Files)]
    public async Task ZeroSizeAndUnavailableDataRemainDistinctAcrossFormats(PullRequestSizeMode mode)
    {
        var start = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var parameters = new ReportParameters(start, new Workspace("team"), new RepoNameFilter(""), [],
            RepoSearchMode.SearchByFilter, PrTimeFilterMode.CreatedOnOnly, [], pullRequestSizeMode: mode,
            teamFilter: "Core");
        var data = new ReportData(parameters, new Dictionary<DisplayName, PersonCsvRow>
        {
            [new DisplayName("Alex Kim")] = new("Senior", "Core")
        });
        data.ObserveDeveloperIdentity(new DeveloperIdentity(new UserUuid("{one}"), new DisplayName("Alex Kim")));
        data.ObserveDeveloperIdentity(new DeveloperIdentity(new UserUuid("{two}"), new DisplayName("Alex Kim")));
        data.ObserveDeveloperIdentity(new DeveloperIdentity(null, new DisplayName("Unknown")));
        data.MutableReports.Add(new PullRequestReport("repo", "repo", "Alice", "main", start, start,
            null, null, PullRequestState.Open, new PullRequestId(1), 0, hasSizeData: true));
        data.MutableReports.Add(new PullRequestReport("repo", "repo", "Bob", "main", start, start,
            null, null, PullRequestState.Open, new PullRequestId(2), 0,
            hasSizeData: false, hasCorrectionsData: false));
        data.MutableReports.Add(new PullRequestReport("repo", "repo", "Carol", "main", start, start,
            null, null, PullRequestState.Open, new PullRequestId(3), 0, filesChanged: 7, linesAdded: 1,
            hasSizeData: true, hasLineSizeData: false));
        data.Freeze();
        var calculator = new StatisticsCalculator();
        var formatter = new DateDiffFormatter();
        var html = new HtmlContentComposer(formatter, calculator).Compose(data);
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var options = new BitbucketOptions
            {
                Workspace = "team",
                PageLength = 10,
                Username = "user",
                AppPassword = "unused",
                RepoNameFilter = "",
                RepoNameList = [],
                BranchNameList = [],
                RepoSearchMode = parameters.RepoSearchMode,
                PrTimeFilterMode = parameters.PrTimeFilterMode,
                Pdf = new PdfOptions { OutputPath = Path.Combine(directory.FullName, "report.pdf") }
            };
            var presenter = new SpectreReportPresenter(
                new SpectreAuthPresenter(), new SpectreRepositoryListPresenter(),
                new SpectreRepositoryAnalysisPresenter(), new SpectreBranchFilterPresenter(),
                new SpectrePullRequestReportPresenter(formatter), new SpectreStatisticsPresenter(calculator, formatter));
            var console = TestConsoleRunner.Run(console =>
            {
                console.Profile.Width = 240;
                presenter.RenderReport(data);
            });
            var renderer = new QuestPdfReportRenderer(Options.Create(options), formatter, calculator, new PdfReportFileStore());
            await renderer.RenderReportAsync(data);
            using var pdf = PdfDocument.Open(Assert.Single(directory.GetFiles("*.pdf")).FullName);
            var pdfText = string.Concat(pdf.GetPages().Select(page => page.Text));

            foreach (var text in new[] { console, WebUtility.HtmlDecode(HtmlTag().Replace(html, "")), pdfText })
            {
                var compact = Layout().Replace(text, "");
                Assert.Contains("XS(0)", compact);
                Assert.Contains("Unavailable", compact);
                Assert.Contains(Layout().Replace(ReportData.IdentityCollisionWarning, ""), compact);
                Assert.Contains(Layout().Replace(ReportData.MissingUserUuidWarning, ""), compact);
                Assert.Contains("AlexKim", compact);
                Assert.Contains("{one}", compact);
                Assert.Contains("{two}", compact);
                if (mode == PullRequestSizeMode.Lines)
                {
                    Assert.DoesNotContain("XS(1)", compact);
                }
                else
                {
                    Assert.Contains("M(7)", compact);
                }
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PublishedReportShowsSameThroughputAndTtfrInAllFormats()
    {
        var start = DateTimeOffset.UnixEpoch;
        var parameters = new ReportParameters(start, new Workspace("team"), new RepoNameFilter(""),
            [], RepoSearchMode.SearchByFilter, PrTimeFilterMode.CreatedOnOnly, []);
        var data = new ReportData(parameters);
        AddReport(1, PullRequestState.Merged, 2);
        AddReport(2, PullRequestState.Merged, 6);
        AddReport(3, PullRequestState.Declined, null);
        AddReport(4, PullRequestState.Open, 20, activityOnly: true);
        data.Freeze();

        var calculator = new StatisticsCalculator();
        var formatter = new DateDiffFormatter();
        var html = new HtmlContentComposer(formatter, calculator).Compose(data);
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var options = new BitbucketOptions
            {
                Workspace = "team",
                PageLength = 10,
                Username = "user",
                AppPassword = "unused",
                RepoNameFilter = "",
                RepoNameList = [],
                BranchNameList = [],
                RepoSearchMode = parameters.RepoSearchMode,
                PrTimeFilterMode = parameters.PrTimeFilterMode,
                Pdf = new PdfOptions { OutputPath = Path.Combine(directory.FullName, "report.pdf") }
            };
            var presenter = new SpectreReportPresenter(
                new SpectreAuthPresenter(), new SpectreRepositoryListPresenter(),
                new SpectreRepositoryAnalysisPresenter(), new SpectreBranchFilterPresenter(),
                new SpectrePullRequestReportPresenter(formatter), new SpectreStatisticsPresenter(calculator, formatter));
            var console = TestConsoleRunner.Run(_ => presenter.RenderReport(data));

            var renderer = new QuestPdfReportRenderer(Options.Create(options), formatter, calculator, new PdfReportFileStore());
            await renderer.RenderReportAsync(data);
            using var pdf = PdfDocument.Open(Assert.Single(directory.GetFiles("*.pdf")).FullName);
            AssertMetrics("console", console);
            AssertMetrics("HTML", WebUtility.HtmlDecode(HtmlTag().Replace(html, "")));
            AssertMetrics("PDF", string.Concat(pdf.GetPages().Select(page => page.Text)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }

        void AddReport(int id, PullRequestState state, int? reactionHours, bool activityOnly = false)
        {
            var end = start.AddHours(22);
            data.MutableReports.Add(new PullRequestReport("repo", "repo", "Alice", "main", start, end,
                state == PullRequestState.Merged ? end : null, state == PullRequestState.Declined ? end : null,
                state, new PullRequestId(id), 0, firstReactionOn: reactionHours.HasValue ? start.AddHours(reactionHours.Value) : null,
                isActivityOnlyMatch: activityOnly));
        }
    }

    private static void AssertMetrics(string format, string text)
    {
        var compact = Layout().Replace(text, "");
        var times = $"BestTTFR{2:0.0}hoursLongestTTFR{6:0.0}hoursMedian{4:0.0}hours75P{5:0.0}hours";
        Assert.True(compact.Contains(times, StringComparison.Ordinal), $"{format}: unexpected TTFR statistics.");
        Assert.True(compact.Contains("PRsCreated3PRsMerged2PRsRejected1", StringComparison.Ordinal),
            $"{format}: expected 3 created, 2 merged and 1 rejected PR.");
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"[\s\u2500-\u257f]")]
    private static partial Regex Layout();
}

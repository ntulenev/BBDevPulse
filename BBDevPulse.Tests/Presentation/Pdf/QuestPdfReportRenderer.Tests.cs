using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;
using BBDevPulse.Presentation.Pdf;

using Microsoft.Extensions.Options;

using Moq;

using QuestPDF.Fluent;

namespace BBDevPulse.Tests.Presentation.Pdf;

public sealed class QuestPdfReportRendererTests
{
    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new QuestPdfReportRenderer(
            options,
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when date formatter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDateFormatterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IDateDiffFormatter formatter = null!;

        // Act
        Action act = () => _ = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(new PdfOptions())),
            formatter,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when statistics calculator is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenStatisticsCalculatorIsNullThrowsArgumentNullException()
    {
        // Arrange
        IStatisticsCalculator calculator = null!;

        // Act
        Action act = () => _ = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(new PdfOptions())),
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            calculator,
            new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when file store is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenFileStoreIsNullThrowsArgumentNullException()
    {
        // Arrange
        IPdfReportFileStore fileStore = null!;

        // Act
        Action act = () => _ = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(new PdfOptions())),
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            fileStore);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderReport throws when report data is null")]
    [Trait("Category", "Unit")]
    public async Task RenderReportAsyncWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var renderer = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(new PdfOptions { Enabled = false })),
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object);
        ReportData reportData = null!;

        // Act
        Func<Task> act = () => renderer.RenderReportAsync(reportData);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderReport returns without saving when PDF generation is disabled")]
    [Trait("Category", "Unit")]
    public async Task RenderReportAsyncWhenPdfIsDisabledDoesNotPersistDocument()
    {
        // Arrange
        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        var saveCalls = 0;
        fileStore.Setup(x => x.SaveAsync(
                It.Is<string>(path => !string.IsNullOrWhiteSpace(path)),
                It.Is<QuestPDF.Infrastructure.IDocument>(document => document != null),
                It.Is<CancellationToken>(token => token == default)))
            .Callback(() => saveCalls++)
            .Returns(Task.CompletedTask);
        var renderer = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(new PdfOptions { Enabled = false })),
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            fileStore.Object);
        var reportData = new ReportData(CreateParameters());

        // Act
        await renderer.RenderReportAsync(reportData);

        // Assert
        saveCalls.Should().Be(0);
    }

    [Fact(DisplayName = "RenderReport saves PDF with empty report data and no configured PDF options")]
    [Trait("Category", "Unit")]
    public async Task RenderReportAsyncWhenNoReportItemsStillSavesDocument()
    {
        // Arrange
        string? savedPath = null;
        var saveCalls = 0;
        var options = CreateOptions(pdf: null!);
        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        fileStore.Setup(x => x.SaveAsync(
                It.Is<string>(path =>
                    Path.GetFileName(path).StartsWith("bbdevpulse-report_", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)),
                It.Is<QuestPDF.Infrastructure.IDocument>(document => document != null),
                It.Is<CancellationToken>(token => token == default)))
            .Callback<string, QuestPDF.Infrastructure.IDocument, CancellationToken>((outputPath, document, cancellationToken) =>
            {
                saveCalls++;
                savedPath = outputPath;
                _ = cancellationToken;
                _ = document.GeneratePdf();
            })
            .Returns(Task.CompletedTask);

        var renderer = new QuestPdfReportRenderer(
            Options.Create(options),
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            fileStore.Object);
        var reportData = new ReportData(CreateParameters());

        // Act
        await renderer.RenderReportAsync(reportData);

        // Assert
        savedPath.Should().NotBeNullOrWhiteSpace();
        Path.GetFileName(savedPath!).Should().MatchRegex(@"^bbdevpulse-report_\d{2}_\d{2}_\d{4}\.pdf$");
        saveCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderReport saves populated report and uses formatter/statistics collaborators")]
    [Trait("Category", "Unit")]
    public async Task RenderReportAsyncWhenDataExistsBuildsAllSectionsAndPersistsPdf()
    {
        // Arrange
        var outputPath = Path.Combine(Path.GetTempPath(), "bbdevpulse-output.pdf");
        var pdfOptions = new PdfOptions
        {
            Enabled = true,
            OutputPath = outputPath
        };
        var dateFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        var formatCalls = 0;
        dateFormatter.Setup(x => x.Format(
                It.Is<DateTimeOffset>(start => start == DateTimeOffset.MinValue),
                It.Is<DateTimeOffset>(end => end >= DateTimeOffset.MinValue)))
            .Callback(() => formatCalls++)
            .Returns("formatted");

        var statistics = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        var percentileCalls = 0;
        statistics.Setup(x => x.Percentile(
                It.Is<IReadOnlyList<double>>(values => values.Count > 0 && values.SequenceEqual(values.OrderBy(v => v))),
                50))
            .Callback(() => percentileCalls++)
            .Returns(1.0);
        statistics.Setup(x => x.Percentile(
                It.Is<IReadOnlyList<double>>(values => values.Count > 0 && values.SequenceEqual(values.OrderBy(v => v))),
                75))
            .Callback(() => percentileCalls++)
            .Returns(2.0);

        string? savedPath = null;
        var saveCalls = 0;
        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        fileStore.Setup(x => x.SaveAsync(
                It.Is<string>(path =>
                    Path.GetFileName(path).StartsWith("bbdevpulse-output_", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)),
                It.Is<QuestPDF.Infrastructure.IDocument>(document => document != null),
                It.Is<CancellationToken>(token => token == default)))
            .Callback<string, QuestPDF.Infrastructure.IDocument, CancellationToken>((path, document, cancellationToken) =>
            {
                saveCalls++;
                savedPath = path;
                _ = cancellationToken;
                _ = document.GeneratePdf();
            })
            .Returns(Task.CompletedTask);

        var renderer = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(pdfOptions)),
            dateFormatter.Object,
            statistics.Object,
            fileStore.Object);
        var reportData = CreatePopulatedReportData();

        // Act
        await renderer.RenderReportAsync(reportData);

        // Assert
        savedPath.Should().NotBeNullOrWhiteSpace();
        Path.GetDirectoryName(savedPath!).Should().Be(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
        Path.GetFileName(savedPath!).Should().MatchRegex(@"^bbdevpulse-output_\d{2}_\d{2}_\d{4}\.pdf$");
        formatCalls.Should().BeGreaterThan(0);
        percentileCalls.Should().BeGreaterThanOrEqualTo(4);
        saveCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderReport saves populated report with developer details enabled")]
    [Trait("Category", "Unit")]
    public async Task RenderReportAsyncWhenDeveloperDetailsEnabledStillBuildsPdf()
    {
        // Arrange
        var outputPath = Path.Combine(Path.GetTempPath(), "bbdevpulse-details.pdf");
        var pdfOptions = new PdfOptions
        {
            Enabled = true,
            OutputPath = outputPath
        };
        var dateFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        dateFormatter.Setup(x => x.Format(
                It.Is<DateTimeOffset>(start => start == DateTimeOffset.MinValue),
                It.Is<DateTimeOffset>(end => end >= DateTimeOffset.MinValue)))
            .Returns("formatted");

        var statistics = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        statistics.Setup(x => x.Percentile(
                It.Is<IReadOnlyList<double>>(values => values.Count > 0 && values.SequenceEqual(values.OrderBy(v => v))),
                50))
            .Returns(1.0);
        statistics.Setup(x => x.Percentile(
                It.Is<IReadOnlyList<double>>(values => values.Count > 0 && values.SequenceEqual(values.OrderBy(v => v))),
                75))
            .Returns(2.0);

        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        fileStore.Setup(x => x.SaveAsync(
                It.Is<string>(path => Path.GetFileName(path).StartsWith("bbdevpulse-details_", StringComparison.OrdinalIgnoreCase)),
                It.Is<QuestPDF.Infrastructure.IDocument>(document => document != null),
                It.IsAny<CancellationToken>()))
            .Callback<string, QuestPDF.Infrastructure.IDocument, CancellationToken>((_, document, _) => _ = document.GeneratePdf())
            .Returns(Task.CompletedTask);

        var renderer = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(pdfOptions)),
            dateFormatter.Object,
            statistics.Object,
            fileStore.Object);
        var reportData = CreateDetailedReportData();

        // Act
        await renderer.RenderReportAsync(reportData);
    }

    [Fact(DisplayName = "BuildWorstMetricSelections chooses distinct pull requests across metrics")]
    [Trait("Category", "Unit")]
    public void BuildWorstMetricSelectionsWhenTopMetricsOverlapUsesDistinctPullRequests()
    {
        // Arrange
        var filterDate = CreateParameters().FilterDate;
        var reports = new List<PullRequestReport>
        {
            new(
                repository: "Repo A",
                repositorySlug: "repo-a",
                author: "Alice",
                targetBranch: "develop",
                createdOn: filterDate,
                lastActivity: filterDate.AddDays(10),
                mergedOn: filterDate.AddDays(10),
                rejectedOn: null,
                state: PullRequestState.Merged,
                id: new PullRequestId(1),
                comments: 1,
                corrections: 10,
                firstReactionOn: filterDate.AddDays(9),
                filesChanged: 4,
                linesAdded: 150,
                linesRemoved: 50),
            new(
                repository: "Repo B",
                repositorySlug: "repo-b",
                author: "Bob",
                targetBranch: "develop",
                createdOn: filterDate,
                lastActivity: filterDate.AddDays(8),
                mergedOn: filterDate.AddDays(8),
                rejectedOn: null,
                state: PullRequestState.Merged,
                id: new PullRequestId(2),
                comments: 1,
                corrections: 3,
                firstReactionOn: filterDate.AddDays(1),
                filesChanged: 2,
                linesAdded: 90,
                linesRemoved: 10),
            new(
                repository: "Repo C",
                repositorySlug: "repo-c",
                author: "Carol",
                targetBranch: "develop",
                createdOn: filterDate,
                lastActivity: filterDate.AddDays(7),
                mergedOn: filterDate.AddDays(2),
                rejectedOn: null,
                state: PullRequestState.Merged,
                id: new PullRequestId(3),
                comments: 1,
                corrections: 2,
                firstReactionOn: filterDate.AddDays(7),
                filesChanged: 3,
                linesAdded: 100,
                linesRemoved: 20),
            new(
                repository: "Repo D",
                repositorySlug: "repo-d",
                author: "Dan",
                targetBranch: "develop",
                createdOn: filterDate,
                lastActivity: filterDate.AddDays(1),
                mergedOn: filterDate.AddDays(1),
                rejectedOn: null,
                state: PullRequestState.Merged,
                id: new PullRequestId(4),
                comments: 1,
                corrections: 1,
                firstReactionOn: filterDate.AddDays(1),
                filesChanged: 8,
                linesAdded: 500,
                linesRemoved: 100)
        };

        // Act
        var selections = QuestPdfReportRenderer.BuildWorstMetricSelections(
            reports,
            excludeWeekend: false,
            excludedDays: new HashSet<DateOnly>());

        // Assert
        selections.Select(selection => selection.MetricName)
            .Should()
            .Equal("Longest Merge Time", "Longest TTFR", "Most Corrections", "Biggest PR");
        selections[0].Report!.Id.Value.Should().Be(1);
        selections[1].Report!.Id.Value.Should().Be(3);
        selections[2].Report!.Id.Value.Should().Be(2);
        selections[3].Report!.Id.Value.Should().Be(4);
        selections.Select(selection => selection.Report!.Id.Value).Distinct().Should().HaveCount(4);
    }

    [Fact(DisplayName = "BuildWorstMetricSelections uses files metric for biggest PR in files mode")]
    [Trait("Category", "Unit")]
    public void BuildWorstMetricSelectionsWhenFilesModeConfiguredUsesFilesMetricForBiggestPr()
    {
        // Arrange
        var filterDate = CreateParameters().FilterDate;
        var reports = new List<PullRequestReport>
        {
            new(
                repository: "Repo A",
                repositorySlug: "repo-a",
                author: "Alice",
                targetBranch: "develop",
                createdOn: filterDate,
                lastActivity: filterDate.AddDays(1),
                mergedOn: null,
                rejectedOn: null,
                state: PullRequestState.Open,
                id: new PullRequestId(1),
                comments: 1,
                corrections: 10,
                firstReactionOn: null,
                filesChanged: 2,
                linesAdded: 900,
                linesRemoved: 500),
            new(
                repository: "Repo B",
                repositorySlug: "repo-b",
                author: "Bob",
                targetBranch: "develop",
                createdOn: filterDate,
                lastActivity: filterDate.AddDays(1),
                mergedOn: null,
                rejectedOn: null,
                state: PullRequestState.Open,
                id: new PullRequestId(2),
                comments: 1,
                corrections: 1,
                firstReactionOn: null,
                filesChanged: 12,
                linesAdded: 50,
                linesRemoved: 10)
        };

        // Act
        var selections = QuestPdfReportRenderer.BuildWorstMetricSelections(
            reports,
            excludeWeekend: false,
            excludedDays: new HashSet<DateOnly>(),
            pullRequestSizeMode: PullRequestSizeMode.Files);

        // Assert
        selections[3].MetricName.Should().Be("Biggest PR");
        selections[3].Report!.Id.Value.Should().Be(2);
        selections[3].Value.Should().Be(12);
    }

    private static BitbucketOptions CreateOptions(PdfOptions pdf)
    {
        return new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace with space",
            PageLength = 25,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Pdf = pdf
        };
    }

    private static ReportParameters CreateParameters()
    {
        return new ReportParameters(
            new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            new Workspace("workspace with space"),
            new RepoNameFilter(string.Empty),
            repoNameList: [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            branchNameList: []);
    }

    private static ReportData CreatePopulatedReportData()
    {
        var reportData = new ReportData(CreateParameters());
        var filterDate = reportData.Parameters.FilterDate;

        reportData.Reports.Add(new PullRequestReport(
            repository: "Repo One",
            repositorySlug: "repo one",
            author: "Alice",
            targetBranch: "develop",
            createdOn: filterDate.AddDays(-1),
            lastActivity: filterDate.AddDays(1),
            mergedOn: filterDate.AddDays(2),
            rejectedOn: null,
            state: PullRequestState.Open,
            id: new PullRequestId(100),
            comments: 3,
            firstReactionOn: filterDate.AddHours(3)));

        reportData.Reports.Add(new PullRequestReport(
            repository: "Repo Two",
            repositorySlug: "repo-two",
            author: "Bob",
            targetBranch: "main",
            createdOn: filterDate.AddDays(2),
            lastActivity: filterDate.AddDays(3),
            mergedOn: null,
            rejectedOn: filterDate.AddDays(4),
            state: PullRequestState.Declined,
            id: new PullRequestId(101),
            comments: 1,
            firstReactionOn: null));

        var developer = reportData.GetOrAddDeveloper(new DeveloperIdentity(
            new UserUuid("{alice-1}"),
            new DisplayName("Alice")));
        developer.PrsOpenedSince = 2;
        developer.PrsMergedAfter = 1;
        developer.CommentsAfter = 4;
        developer.ApprovalsAfter = 5;

        return reportData;
    }

    private static ReportData CreateDetailedReportData()
    {
        var reportData = new ReportData(new ReportParameters(
            new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            new Workspace("workspace with space"),
            new RepoNameFilter(string.Empty),
            repoNameList: [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            branchNameList: [],
            showAllDetailsForDevelopers: true));
        var filterDate = reportData.Parameters.FilterDate;
        var report = new PullRequestReport(
            repository: "Repo One",
            repositorySlug: "repo-one",
            author: "Alice",
            targetBranch: "develop",
            createdOn: filterDate,
            lastActivity: filterDate.AddDays(2),
            mergedOn: filterDate.AddDays(3),
            rejectedOn: null,
            state: PullRequestState.Merged,
            id: new PullRequestId(200),
            comments: 2,
            corrections: 2,
            firstReactionOn: filterDate.AddHours(4),
            filesChanged: 4,
            linesAdded: 60,
            linesRemoved: 20);
        reportData.Reports.Add(report);
        var developer = reportData.GetOrAddDeveloper(new DeveloperIdentity(
            new UserUuid("{alice-1}"),
            new DisplayName("Alice")));
        developer.PrsOpenedSince = 1;
        developer.PrsMergedAfter = 1;
        developer.CommentsAfter = 1;
        developer.ApprovalsAfter = 1;
        developer.Corrections = 2;
        developer.AuthoredPullRequests.Add(report);
        developer.CommentActivities.Add(new DeveloperCommentActivity("Repo One", "repo-one", new PullRequestId(200), "Alice", filterDate.AddHours(5)));
        developer.ApprovalActivities.Add(new DeveloperApprovalActivity("Repo One", "repo-one", new PullRequestId(200), "Alice", filterDate.AddHours(6)));
        developer.CommitActivities.Add(new DeveloperCommitActivity("Repo One", "repo-one", new PullRequestId(200), "abcdef1234567890", filterDate.AddHours(7)));
        return reportData;
    }
}


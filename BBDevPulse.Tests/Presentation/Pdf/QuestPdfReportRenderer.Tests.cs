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
    public void RenderReportWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var renderer = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(new PdfOptions { Enabled = false })),
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object);
        ReportData reportData = null!;

        // Act
        Action act = () => renderer.RenderReport(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderReport returns without saving when PDF generation is disabled")]
    [Trait("Category", "Unit")]
    public void RenderReportWhenPdfIsDisabledDoesNotPersistDocument()
    {
        // Arrange
        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        var saveCalls = 0;
        fileStore.Setup(x => x.Save(
                It.IsAny<string>(),
                It.IsAny<QuestPDF.Infrastructure.IDocument>()))
            .Callback(() => saveCalls++);
        var renderer = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(new PdfOptions { Enabled = false })),
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            fileStore.Object);
        var reportData = new ReportData(CreateParameters());

        // Act
        renderer.RenderReport(reportData);

        // Assert
        saveCalls.Should().Be(0);
    }

    [Fact(DisplayName = "RenderReport saves PDF with empty report data and no configured PDF options")]
    [Trait("Category", "Unit")]
    public void RenderReportWhenNoReportItemsStillSavesDocument()
    {
        // Arrange
        string? savedPath = null;
        var saveCalls = 0;
        var options = CreateOptions(pdf: null!);
        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        fileStore.Setup(x => x.Save(
                It.IsAny<string>(),
                It.IsAny<QuestPDF.Infrastructure.IDocument>()))
            .Callback<string, QuestPDF.Infrastructure.IDocument>((outputPath, document) =>
            {
                saveCalls++;
                savedPath = outputPath;
                _ = document.GeneratePdf();
            });

        var renderer = new QuestPdfReportRenderer(
            Options.Create(options),
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            fileStore.Object);
        var reportData = new ReportData(CreateParameters());

        // Act
        renderer.RenderReport(reportData);

        // Assert
        savedPath.Should().NotBeNullOrWhiteSpace();
        Path.GetFileName(savedPath!).Should().MatchRegex(@"^bbdevpulse-report_\d{2}_\d{2}_\d{4}\.pdf$");
        saveCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderReport saves populated report and uses formatter/statistics collaborators")]
    [Trait("Category", "Unit")]
    public void RenderReportWhenDataExistsBuildsAllSectionsAndPersistsPdf()
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
        dateFormatter.Setup(x => x.Format(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .Callback(() => formatCalls++)
            .Returns("formatted");

        var statistics = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        var percentileCalls = 0;
        statistics.Setup(x => x.Percentile(It.IsAny<IReadOnlyList<double>>(), 50))
            .Callback(() => percentileCalls++)
            .Returns(1.0);
        statistics.Setup(x => x.Percentile(It.IsAny<IReadOnlyList<double>>(), 75))
            .Callback(() => percentileCalls++)
            .Returns(2.0);

        string? savedPath = null;
        var saveCalls = 0;
        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        fileStore.Setup(x => x.Save(
                It.IsAny<string>(),
                It.IsAny<QuestPDF.Infrastructure.IDocument>()))
            .Callback<string, QuestPDF.Infrastructure.IDocument>((path, document) =>
            {
                saveCalls++;
                savedPath = path;
                _ = document.GeneratePdf();
            });

        var renderer = new QuestPdfReportRenderer(
            Options.Create(CreateOptions(pdfOptions)),
            dateFormatter.Object,
            statistics.Object,
            fileStore.Object);
        var reportData = CreatePopulatedReportData();

        // Act
        renderer.RenderReport(reportData);

        // Assert
        savedPath.Should().NotBeNullOrWhiteSpace();
        Path.GetDirectoryName(savedPath!).Should().Be(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
        Path.GetFileName(savedPath!).Should().MatchRegex(@"^bbdevpulse-output_\d{2}_\d{2}_\d{4}\.pdf$");
        formatCalls.Should().BeGreaterThan(0);
        percentileCalls.Should().BeGreaterThanOrEqualTo(4);
        saveCalls.Should().Be(1);
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
}

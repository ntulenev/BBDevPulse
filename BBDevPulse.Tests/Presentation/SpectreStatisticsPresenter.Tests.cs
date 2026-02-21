using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

using Moq;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreStatisticsPresenterTests
{
    [Fact(DisplayName = "Constructor throws when statistics calculator is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenStatisticsCalculatorIsNullThrowsArgumentNullException()
    {
        // Arrange
        IStatisticsCalculator statisticsCalculator = null!;
        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object;

        // Act
        Action act = () => _ = new SpectreStatisticsPresenter(statisticsCalculator, dateDiffFormatter);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when date difference formatter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDateDiffFormatterIsNullThrowsArgumentNullException()
    {
        // Arrange
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object;
        IDateDiffFormatter dateDiffFormatter = null!;

        // Act
        Action act = () => _ = new SpectreStatisticsPresenter(statisticsCalculator, dateDiffFormatter);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderMergeTimeStats throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderMergeTimeStatsWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderMergeTimeStats(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderMergeTimeStats writes no-data message when no merged reports exist")]
    [Trait("Category", "Unit")]
    public void RenderMergeTimeStatsWhenNoMergedReportsExistWritesNoDataMessage()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(1, mergedOn: null, firstReactionOn: null));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderMergeTimeStats(reportData));

        // Assert
        output.Should().Contain("Merge Time Stats");
        output.Should().Contain("No merged pull requests in the report.");
    }

    [Fact(DisplayName = "RenderMergeTimeStats calculates percentiles and formats four metrics")]
    [Trait("Category", "Unit")]
    public void RenderMergeTimeStatsWhenMergedReportsExistCalculatesAndFormatsMetrics()
    {
        // Arrange
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        var p50Calls = 0;
        var p75Calls = 0;
        statisticsCalculator.Setup(x => x.Percentile(It.IsAny<IReadOnlyList<double>>(), 50))
            .Callback(() => p50Calls++)
            .Returns(2.0);
        statisticsCalculator.Setup(x => x.Percentile(It.IsAny<IReadOnlyList<double>>(), 75))
            .Callback(() => p75Calls++)
            .Returns(3.0);

        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        var formatCalls = 0;
        dateDiffFormatter.Setup(x => x.Format(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .Callback(() => formatCalls++)
            .Returns("formatted");

        var presenter = new SpectreStatisticsPresenter(statisticsCalculator.Object, dateDiffFormatter.Object);
        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(1, mergedOn: BaseDate.AddDays(1), firstReactionOn: null));
        reportData.Reports.Add(CreateReport(2, mergedOn: BaseDate.AddDays(3), firstReactionOn: null));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderMergeTimeStats(reportData));

        // Assert
        output.Should().Contain("Merge Time Stats");
        output.Should().Contain("Best Merge Time");
        output.Should().Contain("Longest Merge Time");
        p50Calls.Should().Be(1);
        p75Calls.Should().Be(1);
        formatCalls.Should().Be(4);
    }

    [Fact(DisplayName = "RenderTtfrStats throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderTtfrStatsWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderTtfrStats(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderTtfrStats writes no-data message when no TTFR entries exist")]
    [Trait("Category", "Unit")]
    public void RenderTtfrStatsWhenNoTtfrDataExistsWritesNoDataMessage()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(1, mergedOn: null, firstReactionOn: null));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderTtfrStats(reportData));

        // Assert
        output.Should().Contain("TTFR Stats");
        output.Should().Contain("No TTFR data available in the report.");
    }

    [Fact(DisplayName = "RenderTtfrStats calculates percentiles and formats four metrics")]
    [Trait("Category", "Unit")]
    public void RenderTtfrStatsWhenTtfrDataExistsCalculatesAndFormatsMetrics()
    {
        // Arrange
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        var p50Calls = 0;
        var p75Calls = 0;
        statisticsCalculator.Setup(x => x.Percentile(It.IsAny<IReadOnlyList<double>>(), 50))
            .Callback(() => p50Calls++)
            .Returns(1.0);
        statisticsCalculator.Setup(x => x.Percentile(It.IsAny<IReadOnlyList<double>>(), 75))
            .Callback(() => p75Calls++)
            .Returns(2.0);

        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        var formatCalls = 0;
        dateDiffFormatter.Setup(x => x.Format(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .Callback(() => formatCalls++)
            .Returns("formatted");

        var presenter = new SpectreStatisticsPresenter(statisticsCalculator.Object, dateDiffFormatter.Object);
        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(1, mergedOn: null, firstReactionOn: BaseDate.AddDays(1)));
        reportData.Reports.Add(CreateReport(2, mergedOn: null, firstReactionOn: BaseDate.AddDays(2)));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderTtfrStats(reportData));

        // Assert
        output.Should().Contain("TTFR Stats");
        output.Should().Contain("Best TTFR");
        output.Should().Contain("Longest TTFR");
        p50Calls.Should().Be(1);
        p75Calls.Should().Be(1);
        formatCalls.Should().Be(4);
    }

    [Fact(DisplayName = "RenderDeveloperStatsTable throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderDeveloperStatsTableWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderDeveloperStatsTable(reportData, BaseDate);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderDeveloperStatsTable renders developers sorted by PRs opened and then by name")]
    [Trait("Category", "Unit")]
    public void RenderDeveloperStatsTableWhenStatsExistRendersSortedRows()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = CreateReportData();

        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Bob")))] =
            new DeveloperStats(new DisplayName("Bob"))
            {
                PrsOpenedSince = 1,
                PrsMergedAfter = 1,
                CommentsAfter = 1,
                ApprovalsAfter = 1
            };

        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Alice")))] =
            new DeveloperStats(new DisplayName("Alice"))
            {
                PrsOpenedSince = 2,
                PrsMergedAfter = 3,
                CommentsAfter = 4,
                ApprovalsAfter = 5
            };

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderDeveloperStatsTable(reportData, BaseDate));

        // Assert
        output.Should().Contain("Developer Stats (since 2026-02-20)");
        output.Should().Contain("Alice");
        output.Should().Contain("Bob");
        output.IndexOf("Alice", StringComparison.Ordinal).Should().BeLessThan(output.IndexOf("Bob", StringComparison.Ordinal));
    }

    private static SpectreStatisticsPresenter CreatePresenter()
    {
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object;
        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object;
        return new SpectreStatisticsPresenter(statisticsCalculator, dateDiffFormatter);
    }

    private static ReportData CreateReportData()
    {
        return new ReportData(new ReportParameters(
            BaseDate,
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            []));
    }

    private static PullRequestReport CreateReport(int id, DateTimeOffset? mergedOn, DateTimeOffset? firstReactionOn)
    {
        return new PullRequestReport(
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            targetBranch: "develop",
            createdOn: BaseDate,
            lastActivity: BaseDate.AddHours(1),
            mergedOn: mergedOn,
            rejectedOn: null,
            state: PullRequestState.Open,
            id: new PullRequestId(id),
            comments: 0,
            firstReactionOn: firstReactionOn);
    }

    private static readonly DateTimeOffset BaseDate = new(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
}

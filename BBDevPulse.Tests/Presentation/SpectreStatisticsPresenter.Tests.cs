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

    [Fact(DisplayName = "RenderPrThroughputStats throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderPrThroughputStatsWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderPrThroughputStats(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderPrThroughputStats renders created merged and rejected pull request counts")]
    [Trait("Category", "Unit")]
    public void RenderPrThroughputStatsWhenDataExistsRendersCreatedMergedAndRejectedCounts()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = new ReportData(new ReportParameters(
            BaseDate,
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            [],
            toDateExclusive: BaseDate.AddDays(4)));

        reportData.Reports.Add(CreateReport(
            id: 1,
            mergedOn: BaseDate.AddDays(1),
            firstReactionOn: null,
            rejectedOn: BaseDate.AddDays(2)));
        reportData.Reports.Add(CreateReport(
            id: 2,
            mergedOn: BaseDate.AddDays(5),
            firstReactionOn: null,
            rejectedOn: null));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoB",
            repositorySlug: "repob",
            author: "External",
            targetBranch: "develop",
            createdOn: BaseDate.AddDays(1),
            lastActivity: BaseDate.AddDays(1).AddHours(1),
            mergedOn: BaseDate.AddDays(2),
            rejectedOn: BaseDate.AddDays(2),
            state: PullRequestState.Merged,
            id: new PullRequestId(3),
            comments: 0,
            isActivityOnlyMatch: true));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderPrThroughputStats(reportData));

        // Assert
        output.Should().Contain("PR Throughput");
        output.Should().Contain("PRs Created");
        output.Should().Contain("PRs Merged");
        output.Should().Contain("PRs Rejected");
        output.Should().Contain("2");
        output.Should().Contain("1");
    }

    [Fact(DisplayName = "RenderMergeTimeStats throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderPrsPerDeveloperStatsWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderPrsPerDeveloperStats(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderPrsPerDeveloperStats writes no-data message when no authored PR data exists")]
    [Trait("Category", "Unit")]
    public void RenderPrsPerDeveloperStatsWhenNoAuthoredPrDataExistsWritesNoDataMessage()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = CreateReportData();

        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Reviewer")))] =
            new DeveloperStats(new DisplayName("Reviewer"))
            {
                PrsOpenedSince = 0,
                CommentsAfter = 3
            };

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderPrsPerDeveloperStats(reportData));

        // Assert
        output.Should().Contain("PRs per Developer");
        output.Should().Contain("No authored pull request data available in the report.");
    }

    [Fact(DisplayName = "RenderPrsPerDeveloperStats calculates percentiles from authored PR counts")]
    [Trait("Category", "Unit")]
    public void RenderPrsPerDeveloperStatsWhenDataExistsCalculatesAndRendersMetrics()
    {
        // Arrange
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        var p50Calls = 0;
        var p75Calls = 0;
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback(() => p50Calls++)
            .Returns(2.0);
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Callback(() => p75Calls++)
            .Returns(3.0);

        var presenter = new SpectreStatisticsPresenter(
            statisticsCalculator.Object,
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object);
        var reportData = CreateReportData();

        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Alice")))] =
            new DeveloperStats(new DisplayName("Alice")) { PrsOpenedSince = 1 };
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Bob")))] =
            new DeveloperStats(new DisplayName("Bob")) { PrsOpenedSince = 4 };
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Reviewer")))] =
            new DeveloperStats(new DisplayName("Reviewer")) { PrsOpenedSince = 0, CommentsAfter = 2 };

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderPrsPerDeveloperStats(reportData));

        // Assert
        output.Should().Contain("PRs per Developer");
        output.Should().Contain("Min PRs/Developer");
        output.Should().Contain("Max PRs/Developer");
        output.Should().Contain("Median");
        output.Should().Contain("75P");
        p50Calls.Should().Be(1);
        p75Calls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderCommentsStats throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderCommentsStatsWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderCommentsStats(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderCommentsStats calculates percentiles and renders comment metrics")]
    [Trait("Category", "Unit")]
    public void RenderCommentsStatsWhenDataExistsCalculatesAndRendersMetrics()
    {
        // Arrange
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        var p50Calls = 0;
        var p75Calls = 0;
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback(() => p50Calls++)
            .Returns(2.0);
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Callback(() => p75Calls++)
            .Returns(3.0);

        var presenter = new SpectreStatisticsPresenter(
            statisticsCalculator.Object,
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object);
        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(1, mergedOn: null, firstReactionOn: null, comments: 1));
        reportData.Reports.Add(CreateReport(2, mergedOn: null, firstReactionOn: null, comments: 4));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderCommentsStats(reportData));

        // Assert
        output.Should().Contain("Comments Stats");
        output.Should().Contain("Min Comments");
        output.Should().Contain("Max Comments");
        p50Calls.Should().Be(1);
        p75Calls.Should().Be(1);
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
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback(() => p50Calls++)
            .Returns(2.0);
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Callback(() => p75Calls++)
            .Returns(3.0);

        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        var formatCalls = 0;
        dateDiffFormatter.Setup(x => x.Format(It.Is<DateTimeOffset>(start => IsMinValueStart(start)), It.Is<DateTimeOffset>(end => IsNonNegativeDurationEnd(end))))
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

    [Fact(DisplayName = "RenderMergeTimeStats excludes weekends from merge durations when configured")]
    [Trait("Category", "Unit")]
    public void RenderMergeTimeStatsWhenExcludeWeekendEnabledUsesWorkingDurationDays()
    {
        // Arrange
        IReadOnlyList<double>? capturedValues = null;
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        statisticsCalculator
            .Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback<IReadOnlyList<double>, int>((values, _) => capturedValues = values)
            .Returns(1.0);
        statisticsCalculator
            .Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Returns(1.0);

        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        dateDiffFormatter
            .Setup(x => x.Format(It.Is<DateTimeOffset>(start => IsMinValueStart(start)), It.Is<DateTimeOffset>(end => IsNonNegativeDurationEnd(end))))
            .Returns("formatted");

        var presenter = new SpectreStatisticsPresenter(statisticsCalculator.Object, dateDiffFormatter.Object);
        var reportData = CreateReportData(excludeWeekend: true);
        var created = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero); // Friday
        var merged = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero); // Monday
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            targetBranch: "develop",
            createdOn: created,
            lastActivity: merged,
            mergedOn: merged,
            rejectedOn: null,
            state: PullRequestState.Merged,
            id: new PullRequestId(5),
            comments: 0,
            firstReactionOn: null));

        // Act
        _ = TestConsoleRunner.Run(_ => presenter.RenderMergeTimeStats(reportData));

        // Assert
        capturedValues.Should().NotBeNull();
        capturedValues!.Single().Should().Be(1.0);
    }

    [Fact(DisplayName = "RenderMergeTimeStats excludes configured days from merge durations")]
    [Trait("Category", "Unit")]
    public void RenderMergeTimeStatsWhenExcludedDaysConfiguredUsesWorkingDurationDays()
    {
        // Arrange
        IReadOnlyList<double>? capturedValues = null;
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        statisticsCalculator
            .Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback<IReadOnlyList<double>, int>((values, _) => capturedValues = values)
            .Returns(1.0);
        statisticsCalculator
            .Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Returns(1.0);

        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        dateDiffFormatter
            .Setup(x => x.Format(It.Is<DateTimeOffset>(start => IsMinValueStart(start)), It.Is<DateTimeOffset>(end => IsNonNegativeDurationEnd(end))))
            .Returns("formatted");

        var presenter = new SpectreStatisticsPresenter(statisticsCalculator.Object, dateDiffFormatter.Object);
        var reportData = CreateReportData(excludedDays: [new DateOnly(2026, 2, 3)]);
        var created = new DateTimeOffset(2026, 2, 2, 12, 0, 0, TimeSpan.Zero); // Monday
        var merged = new DateTimeOffset(2026, 2, 4, 12, 0, 0, TimeSpan.Zero); // Wednesday
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            targetBranch: "develop",
            createdOn: created,
            lastActivity: merged,
            mergedOn: merged,
            rejectedOn: null,
            state: PullRequestState.Merged,
            id: new PullRequestId(6),
            comments: 0,
            firstReactionOn: null));

        // Act
        _ = TestConsoleRunner.Run(_ => presenter.RenderMergeTimeStats(reportData));

        // Assert
        capturedValues.Should().NotBeNull();
        capturedValues!.Single().Should().Be(1.0);
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
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback(() => p50Calls++)
            .Returns(1.0);
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Callback(() => p75Calls++)
            .Returns(2.0);

        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        var formatCalls = 0;
        dateDiffFormatter.Setup(x => x.Format(It.Is<DateTimeOffset>(start => IsMinValueStart(start)), It.Is<DateTimeOffset>(end => IsNonNegativeDurationEnd(end))))
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

    [Fact(DisplayName = "RenderCorrectionsStats throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderCorrectionsStatsWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderCorrectionsStats(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderCorrectionsStats calculates percentiles and renders correction metrics")]
    [Trait("Category", "Unit")]
    public void RenderCorrectionsStatsWhenDataExistsCalculatesAndRendersMetrics()
    {
        // Arrange
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        var p50Calls = 0;
        var p75Calls = 0;
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback(() => p50Calls++)
            .Returns(1.0);
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Callback(() => p75Calls++)
            .Returns(2.0);
        var presenter = new SpectreStatisticsPresenter(
            statisticsCalculator.Object,
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object);
        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(1, mergedOn: null, firstReactionOn: null, corrections: 0));
        reportData.Reports.Add(CreateReport(2, mergedOn: null, firstReactionOn: null, corrections: 3));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderCorrectionsStats(reportData));

        // Assert
        output.Should().Contain("Corrections Stats");
        output.Should().Contain("Min Corrections");
        output.Should().Contain("Max Corrections");
        p50Calls.Should().Be(1);
        p75Calls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderPullRequestSizeStats throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderPullRequestSizeStatsWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderPullRequestSizeStats(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderPullRequestSizeStats writes no-data message when no PR size data exists")]
    [Trait("Category", "Unit")]
    public void RenderPullRequestSizeStatsWhenNoSizeDataExistsWritesNoDataMessage()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(1, mergedOn: null, firstReactionOn: null));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderPullRequestSizeStats(reportData));

        // Assert
        output.Should().Contain("PR Size Stats");
        output.Should().Contain("No PR size data available in the report.");
    }

    [Fact(DisplayName = "RenderPullRequestSizeStats calculates percentiles and renders size metrics")]
    [Trait("Category", "Unit")]
    public void RenderPullRequestSizeStatsWhenDataExistsCalculatesAndRendersMetrics()
    {
        // Arrange
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        var p50Calls = 0;
        var p75Calls = 0;
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback(() => p50Calls++)
            .Returns(150.0);
        statisticsCalculator.Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Callback(() => p75Calls++)
            .Returns(190.0);

        var presenter = new SpectreStatisticsPresenter(
            statisticsCalculator.Object,
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object);

        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(
            1,
            mergedOn: null,
            firstReactionOn: null,
            filesChanged: 3,
            linesAdded: 100,
            linesRemoved: 20));
        reportData.Reports.Add(CreateReport(
            2,
            mergedOn: null,
            firstReactionOn: null,
            filesChanged: 5,
            linesAdded: 180,
            linesRemoved: 20));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderPullRequestSizeStats(reportData));

        // Assert
        output.Should().Contain("PR Size Stats");
        output.Should().Contain("Smallest PR");
        output.Should().Contain("Biggest PR");
        p50Calls.Should().Be(1);
        p75Calls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderPullRequestSizeStats uses file counts in files mode")]
    [Trait("Category", "Unit")]
    public void RenderPullRequestSizeStatsWhenFilesModeConfiguredUsesFilesMetric()
    {
        // Arrange
        IReadOnlyList<double>? capturedValues = null;
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        statisticsCalculator
            .Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback<IReadOnlyList<double>, int>((values, _) => capturedValues = values)
            .Returns(4.0);
        statisticsCalculator
            .Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Returns(9.0);

        var presenter = new SpectreStatisticsPresenter(
            statisticsCalculator.Object,
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object);

        var reportData = CreateReportData(pullRequestSizeMode: PullRequestSizeMode.Files);
        reportData.Reports.Add(CreateReport(
            id: 1,
            mergedOn: null,
            firstReactionOn: null,
            filesChanged: 4,
            linesAdded: 1000,
            linesRemoved: 400));
        reportData.Reports.Add(CreateReport(
            id: 2,
            mergedOn: null,
            firstReactionOn: null,
            filesChanged: 9,
            linesAdded: 3000,
            linesRemoved: 700));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderPullRequestSizeStats(reportData));

        // Assert
        output.Should().Contain("PR Size Stats");
        output.Should().Contain("Files");
        capturedValues.Should().NotBeNull();
        capturedValues!.Should().Equal(4.0, 9.0);
    }

    [Fact(DisplayName = "RenderCorrectionsStats ignores activity-only PR rows")]
    [Trait("Category", "Unit")]
    public void RenderCorrectionsStatsWhenActivityOnlyRowsExistExcludesThemFromMetrics()
    {
        // Arrange
        IReadOnlyList<double>? capturedValues = null;
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict);
        statisticsCalculator
            .Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 50))
            .Callback<IReadOnlyList<double>, int>((values, _) => capturedValues = values)
            .Returns(1.0);
        statisticsCalculator
            .Setup(x => x.Percentile(It.Is<IReadOnlyList<double>>(values => IsOrderedNonEmpty(values)), 75))
            .Returns(1.0);

        var presenter = new SpectreStatisticsPresenter(
            statisticsCalculator.Object,
            new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object);

        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(
            id: 1,
            mergedOn: null,
            firstReactionOn: null,
            corrections: 2));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoB",
            repositorySlug: "repob",
            author: "External",
            targetBranch: "develop",
            createdOn: BaseDate,
            lastActivity: BaseDate.AddHours(1),
            mergedOn: null,
            rejectedOn: null,
            state: PullRequestState.Open,
            id: new PullRequestId(2),
            comments: 0,
            corrections: 100,
            firstReactionOn: null,
            isActivityOnlyMatch: true));

        // Act
        _ = TestConsoleRunner.Run(_ => presenter.RenderCorrectionsStats(reportData));

        // Assert
        capturedValues.Should().NotBeNull();
        capturedValues!.Should().Equal(2.0);
    }

    [Fact(DisplayName = "RenderWorstPullRequestsTable throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderWorstPullRequestsTableWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderWorstPullRequestsTable(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderWorstPullRequestsTable writes no-data message when no PR entries exist")]
    [Trait("Category", "Unit")]
    public void RenderWorstPullRequestsTableWhenNoReportsExistWritesNoDataMessage()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = CreateReportData();

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderWorstPullRequestsTable(reportData));

        // Assert
        output.Should().Contain("Worst PRs by Metric");
        output.Should().Contain("No pull requests available to calculate worst metrics.");
    }

    [Fact(DisplayName = "RenderWorstPullRequestsTable chooses distinct PRs for each metric")]
    [Trait("Category", "Unit")]
    public void RenderWorstPullRequestsTableWhenSamePrLeadsMultipleMetricsUsesDistinctEntries()
    {
        // Arrange
        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        dateDiffFormatter
            .Setup(x => x.Format(It.Is<DateTimeOffset>(start => IsMinValueStart(start)), It.Is<DateTimeOffset>(end => IsNonNegativeDurationEnd(end))))
            .Returns("formatted");

        var presenter = new SpectreStatisticsPresenter(
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            dateDiffFormatter.Object);

        var reportData = CreateReportData();
        reportData.Reports.Add(CreateReport(
            id: 1,
            mergedOn: BaseDate.AddDays(10),
            firstReactionOn: BaseDate.AddDays(9),
            corrections: 10,
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            filesChanged: 4,
            linesAdded: 150,
            linesRemoved: 50));
        reportData.Reports.Add(CreateReport(
            id: 2,
            mergedOn: BaseDate.AddDays(8),
            firstReactionOn: BaseDate.AddDays(1),
            corrections: 1,
            repository: "RepoB",
            repositorySlug: "repob",
            author: "Bob",
            filesChanged: 3,
            linesAdded: 100,
            linesRemoved: 20));
        reportData.Reports.Add(CreateReport(
            id: 3,
            mergedOn: BaseDate.AddDays(2),
            firstReactionOn: BaseDate.AddDays(7),
            corrections: 2,
            repository: "RepoC",
            repositorySlug: "repoc",
            author: "Carol",
            filesChanged: 2,
            linesAdded: 90,
            linesRemoved: 10));
        reportData.Reports.Add(CreateReport(
            id: 4,
            mergedOn: BaseDate.AddDays(1),
            firstReactionOn: BaseDate.AddDays(1),
            corrections: 0,
            repository: "RepoD",
            repositorySlug: "repod",
            author: "Dan",
            filesChanged: 8,
            linesAdded: 500,
            linesRemoved: 100));
        reportData.Reports.Add(CreateReport(
            id: 5,
            mergedOn: BaseDate.AddDays(3),
            firstReactionOn: BaseDate.AddDays(2),
            comments: 50,
            corrections: 0,
            repository: "RepoE",
            repositorySlug: "repoe",
            author: "Eve",
            filesChanged: 1,
            linesAdded: 10,
            linesRemoved: 5));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderWorstPullRequestsTable(reportData));

        // Assert
        output.Should().Contain("Worst PRs by Metric");
        output.Should().Contain("Longest Merge Time");
        output.Should().Contain("Longest TTFR");
        output.Should().Contain("Most Comments");
        output.Should().Contain("Most Corrections");
        output.Should().Contain("Biggest PR");
        output.Should().Contain("RepoA");
        output.Should().Contain("RepoC");
        output.Should().Contain("RepoB");
        output.Should().Contain("RepoD");
        output.Should().Contain("RepoE");
        output.IndexOf("RepoA", StringComparison.Ordinal).Should().BeLessThan(output.IndexOf("RepoC", StringComparison.Ordinal));
        output.IndexOf("RepoC", StringComparison.Ordinal).Should().BeLessThan(output.IndexOf("RepoE", StringComparison.Ordinal));
        output.IndexOf("RepoE", StringComparison.Ordinal).Should().BeLessThan(output.IndexOf("RepoB", StringComparison.Ordinal));
        output.IndexOf("RepoB", StringComparison.Ordinal).Should().BeLessThan(output.IndexOf("RepoD", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "RenderWorstPullRequestsTable uses files metric for biggest PR in files mode")]
    [Trait("Category", "Unit")]
    public void RenderWorstPullRequestsTableWhenFilesModeConfiguredUsesFilesMetricForBiggestPr()
    {
        // Arrange
        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        dateDiffFormatter
            .Setup(x => x.Format(It.Is<DateTimeOffset>(start => IsMinValueStart(start)), It.Is<DateTimeOffset>(end => IsNonNegativeDurationEnd(end))))
            .Returns("formatted");

        var presenter = new SpectreStatisticsPresenter(
            new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object,
            dateDiffFormatter.Object);

        var reportData = CreateReportData(pullRequestSizeMode: PullRequestSizeMode.Files);
        reportData.Reports.Add(CreateReport(
            id: 11,
            mergedOn: null,
            firstReactionOn: null,
            comments: 2,
            corrections: 10,
            repository: "RepoCorrections",
            repositorySlug: "repo-corrections",
            author: "Alice",
            filesChanged: 2,
            linesAdded: 3000,
            linesRemoved: 700));
        reportData.Reports.Add(CreateReport(
            id: 12,
            mergedOn: null,
            firstReactionOn: null,
            comments: 3,
            corrections: 1,
            repository: "RepoFilesBiggest",
            repositorySlug: "repo-files-biggest",
            author: "Bob",
            filesChanged: 12,
            linesAdded: 100,
            linesRemoved: 20));
        reportData.Reports.Add(CreateReport(
            id: 13,
            mergedOn: null,
            firstReactionOn: null,
            comments: 20,
            corrections: 0,
            repository: "RepoComments",
            repositorySlug: "repo-comments",
            author: "Carol",
            filesChanged: 1,
            linesAdded: 5,
            linesRemoved: 1));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderWorstPullRequestsTable(reportData));

        // Assert
        output.Should().Contain("Most Comments");
        output.Should().Contain("RepoComments");
        output.Should().Contain("Biggest PR");
        output.Should().Contain("RepoFilesBiggest");
        output.Should().Contain("12 (L)");
    }

    [Fact(DisplayName = "RenderDeveloperStatsTable throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderDeveloperStatsTableWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderDeveloperStatsTable(reportData);

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
                Grade = "Senior",
                Department = "Platform",
                PrsOpenedSince = 2,
                PrsMergedAfter = 3,
                CommentsAfter = 4,
                ApprovalsAfter = 5
            };

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderDeveloperStatsTable(reportData));

        // Assert
        output.Should().Contain("Developer Stats (since 2026-02-20)");
        output.Should().Contain("Alice");
        output.Should().Contain("Bob");
        output.Should().Contain("Grade");
        output.Should().Contain("Senior");
        output.Should().Contain(DeveloperStats.NOT_AVAILABLE);
        output.IndexOf("Alice", StringComparison.Ordinal).Should().BeLessThan(output.IndexOf("Bob", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "RenderDeveloperStatsTable shows UUID column when configured")]
    [Trait("Category", "Unit")]
    public void RenderDeveloperStatsTableWhenUuidOptionEnabledRendersUuidValues()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = CreateReportData(showDeveloperUuidInStats: true);

        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(new UserUuid("{alice-1}"), new DisplayName("Alice")))] =
            new DeveloperStats(new DisplayName("Alice"), new UserUuid("{alice-1}"))
            {
                Grade = "Senior",
                Department = "Platform",
                PrsOpenedSince = 1
            };

        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Bob")))] =
            new DeveloperStats(new DisplayName("Bob"))
            {
                Department = "Platform"
            };

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderDeveloperStatsTable(reportData));

        // Assert
        output.Should().Contain("UUID");
        output.Should().Contain("{alice");
        output.Should().Contain("-1}");
        output.Should().Contain(DeveloperStats.NOT_AVAILABLE);
    }

    [Fact(DisplayName = "RenderDeveloperStatsTable uses bounded date window label when upper bound exists")]
    [Trait("Category", "Unit")]
    public void RenderDeveloperStatsTableWhenUpperBoundConfiguredShowsDateWindow()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = new ReportData(new ReportParameters(
            new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            [],
            toDateExclusive: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));

        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Alice")))] =
            new DeveloperStats(new DisplayName("Alice"));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderDeveloperStatsTable(reportData));

        // Assert
        output.Should().Contain("Developer Stats (2026-02-20 to 2026-02-28)");
    }

    [Fact(DisplayName = "RenderDeveloperDetails renders authored PRs comments approvals and follow-up commits")]
    [Trait("Category", "Unit")]
    public void RenderDeveloperDetailsWhenDetailsExistRendersAllSections()
    {
        // Arrange
        var presenter = CreatePresenter();
        var reportData = CreateReportData(showAllDetailsForDevelopers: true);
        var developer = new DeveloperStats(new DisplayName("Alice"));
        var report = CreateReport(
            id: 7,
            mergedOn: BaseDate.AddDays(2),
            firstReactionOn: BaseDate.AddDays(1),
            corrections: 2,
            filesChanged: 3,
            linesAdded: 30,
            linesRemoved: 20);
        developer.AuthoredPullRequests.Add(report);
        developer.CommentActivities.Add(new DeveloperCommentActivity("RepoA", "repoa", new PullRequestId(7), "Bob", BaseDate.AddHours(3)));
        developer.ApprovalActivities.Add(new DeveloperApprovalActivity("RepoA", "repoa", new PullRequestId(7), "Bob", BaseDate.AddHours(4)));
        developer.CommitActivities.Add(new DeveloperCommitActivity(
            "RepoA",
            "repoa",
            new PullRequestId(7),
            "abcdef1234567890",
            "Add follow-up commit size and message",
            BaseDate.AddHours(5),
            new PullRequestSizeSummary(FilesChanged: 17, LinesAdded: 80, LinesRemoved: 43)));
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Alice")))] = developer;

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderDeveloperDetails(reportData));

        // Assert
        output.Should().Contain("Developer Details");
        output.Should().Contain("Authored PRs");
        output.Should().Contain("Comments");
        output.Should().Contain("Approvals");
        output.Should().Contain("Follow-up Commits");
        output.Should().Contain("Alice");
        output.Should().Contain("RepoA");
        output.Should().Contain("Message");
        output.Should().Contain("Add follow-up");
        output.Should().Contain("commit size and");
        output.Should().Contain("message");
        output.Should().Contain("Churn");
        output.Should().Contain("123");
        output.Should().Contain("abcdef123456");
    }

    [Fact(DisplayName = "RenderBitbucketTelemetry throws when snapshot is null")]
    [Trait("Category", "Unit")]
    public void RenderBitbucketTelemetryWhenSnapshotIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
        BitbucketTelemetrySnapshot telemetrySnapshot = null!;

        // Act
        Action act = () => presenter.RenderBitbucketTelemetry(telemetrySnapshot);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderBitbucketTelemetry writes nothing when telemetry is disabled")]
    [Trait("Category", "Unit")]
    public void RenderBitbucketTelemetryWhenTelemetryDisabledWritesNothing()
    {
        // Arrange
        var presenter = CreatePresenter();
        var telemetrySnapshot = new BitbucketTelemetrySnapshot(false, 0, 0, 0, 0, 0, []);

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderBitbucketTelemetry(telemetrySnapshot));

        // Assert
        output.Should().BeEmpty();
    }

    [Fact(DisplayName = "RenderBitbucketTelemetry renders summary and request breakdown")]
    [Trait("Category", "Unit")]
    public void RenderBitbucketTelemetryWhenTelemetryEnabledRendersSummaryAndRequests()
    {
        // Arrange
        var presenter = CreatePresenter();
        var telemetrySnapshot = new BitbucketTelemetrySnapshot(
            true,
            10,
            3,
            4,
            2,
            5,
            [
                new BitbucketApiRequestStatistic("GET /user", 4),
                new BitbucketApiRequestStatistic("GET /repositories/{workspace}/{repository}/pullrequests/{pullRequestId}/activity", 2)
            ]);

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderBitbucketTelemetry(telemetrySnapshot));

        // Assert
        output.Should().Contain("Bitbucket Telemetry");
        output.Should().Contain("HTTP Requests");
        output.Should().Contain("Analysis Cache Hits");
        output.Should().Contain("Analysis Cache Misses");
        output.Should().Contain("Analysis Cache Stores");
        output.Should().Contain("Estimated Avoided Requests");
        output.Should().Contain("Estimated Cache Efficiency");
        output.Should().Contain("33.3 %");
        output.Should().Contain("GET /user");
        output.Should().Contain("/repositories/{workspace}/{repository}/pullrequests/{pullRequestI");
        output.Should().Contain("d}/activity");
    }

    private static SpectreStatisticsPresenter CreatePresenter()
    {
        var statisticsCalculator = new Mock<IStatisticsCalculator>(MockBehavior.Strict).Object;
        var dateDiffFormatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object;
        return new SpectreStatisticsPresenter(statisticsCalculator, dateDiffFormatter);
    }

    private static ReportData CreateReportData(
        bool excludeWeekend = false,
        IReadOnlyList<DateOnly>? excludedDays = null,
        PullRequestSizeMode pullRequestSizeMode = PullRequestSizeMode.Lines,
        bool showDeveloperUuidInStats = false,
        bool showAllDetailsForDevelopers = false)
    {
        return new ReportData(new ReportParameters(
            BaseDate,
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            [],
            excludeWeekend,
            excludedDays,
            pullRequestSizeMode,
            showDeveloperUuidInStats: showDeveloperUuidInStats,
            showAllDetailsForDevelopers: showAllDetailsForDevelopers));
    }

    private static PullRequestReport CreateReport(
        int id,
        DateTimeOffset? mergedOn,
        DateTimeOffset? firstReactionOn,
        int comments = 0,
        int corrections = 0,
        string repository = "RepoA",
        string repositorySlug = "repoa",
        string author = "Alice",
        DateTimeOffset? rejectedOn = null,
        int filesChanged = 0,
        int linesAdded = 0,
        int linesRemoved = 0)
    {
        return new PullRequestReport(
            repository: repository,
            repositorySlug: repositorySlug,
            author: author,
            targetBranch: "develop",
            createdOn: BaseDate,
            lastActivity: BaseDate.AddHours(1),
            mergedOn: mergedOn,
            rejectedOn: rejectedOn,
            state: PullRequestState.Open,
            id: new PullRequestId(id),
            comments: comments,
            corrections: corrections,
            firstReactionOn: firstReactionOn,
            filesChanged: filesChanged,
            linesAdded: linesAdded,
            linesRemoved: linesRemoved);
    }

    private static bool IsOrderedNonEmpty(IReadOnlyList<double> values) =>
        values.Count > 0 && values.SequenceEqual(values.OrderBy(static value => value));

    private static bool IsMinValueStart(DateTimeOffset start) =>
        start == DateTimeOffset.MinValue;

    private static bool IsNonNegativeDurationEnd(DateTimeOffset end) =>
        end >= DateTimeOffset.MinValue;

    private static readonly DateTimeOffset BaseDate = new(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
}



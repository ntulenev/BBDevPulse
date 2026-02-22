using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

using Moq;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectrePullRequestReportPresenterTests
{
    [Fact(DisplayName = "Constructor throws when date difference formatter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDateDiffFormatterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IDateDiffFormatter dateDiffFormatter = null!;

        // Act
        Action act = () => _ = new SpectrePullRequestReportPresenter(dateDiffFormatter);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderPullRequestTable throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderPullRequestTableWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var formatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict).Object;
        var presenter = new SpectrePullRequestReportPresenter(formatter);
        ReportData reportData = null!;

        // Act
        Action act = () => presenter.RenderPullRequestTable(reportData, DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderPullRequestTable writes pull request table and uses date formatter")]
    [Trait("Category", "Unit")]
    public void RenderPullRequestTableWhenReportsExistRendersTableAndFormatsDurations()
    {
        // Arrange
        var formatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        var formatCalls = 0;
        formatter.Setup(x => x.Format(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .Callback(() => formatCalls++)
            .Returns("formatted");

        var presenter = new SpectrePullRequestReportPresenter(formatter.Object);
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateReportParameters(createdOn.AddDays(-1)));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            targetBranch: "develop",
            createdOn: createdOn,
            lastActivity: createdOn.AddHours(1),
            mergedOn: createdOn.AddHours(2),
            rejectedOn: null,
            state: PullRequestState.Merged,
            id: new PullRequestId(11),
            comments: 4,
            firstReactionOn: createdOn.AddMinutes(30)));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderPullRequestTable(reportData, createdOn.AddDays(1)));

        // Assert
        output.Should().Contain("Pull Requests");
        output.Should().Contain("11");
        output.Should().Contain("4");
        formatCalls.Should().Be(2);
    }

    [Fact(DisplayName = "RenderPullRequestTable renders fallback markers when merge and TTFR are missing")]
    [Trait("Category", "Unit")]
    public void RenderPullRequestTableWhenOptionalDatesAreMissingRendersDashFallbacks()
    {
        // Arrange
        var formatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        var formatCalls = 0;
        formatter.Setup(x => x.Format(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .Callback(() => formatCalls++)
            .Returns("formatted");

        var presenter = new SpectrePullRequestReportPresenter(formatter.Object);
        var filterDate = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateReportParameters(filterDate));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            targetBranch: "develop",
            createdOn: filterDate.AddDays(-1),
            lastActivity: filterDate.AddDays(1),
            mergedOn: null,
            rejectedOn: null,
            state: PullRequestState.Open,
            id: new PullRequestId(21),
            comments: 1,
            firstReactionOn: null));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoB",
            repositorySlug: "repob",
            author: "Bob",
            targetBranch: "main",
            createdOn: filterDate.AddDays(1),
            lastActivity: filterDate.AddDays(2),
            mergedOn: filterDate.AddDays(3),
            rejectedOn: filterDate.AddDays(4),
            state: PullRequestState.Declined,
            id: new PullRequestId(22),
            comments: 2,
            firstReactionOn: null));

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderPullRequestTable(reportData, filterDate));

        // Assert
        output.Should().Contain("21");
        output.Should().Contain("22");
        output.Should().Contain("-");
        formatCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderPullRequestTable excludes weekend hours from TTFR and merge time when configured")]
    [Trait("Category", "Unit")]
    public void RenderPullRequestTableWhenExcludeWeekendIsEnabledUsesWorkingDuration()
    {
        // Arrange
        var formatter = new Mock<IDateDiffFormatter>(MockBehavior.Strict);
        List<double> formattedDurations = [];
        formatter.Setup(x => x.Format(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .Callback<DateTimeOffset, DateTimeOffset>((start, end) => formattedDurations.Add((end - start).TotalDays))
            .Returns("formatted");
        var presenter = new SpectrePullRequestReportPresenter(formatter.Object);
        var createdOn = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero); // Friday
        var mondayNoon = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero); // Monday
        var reportData = new ReportData(CreateReportParameters(createdOn.AddDays(-1), excludeWeekend: true));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            targetBranch: "develop",
            createdOn: createdOn,
            lastActivity: mondayNoon,
            mergedOn: mondayNoon,
            rejectedOn: null,
            state: PullRequestState.Merged,
            id: new PullRequestId(31),
            comments: 1,
            firstReactionOn: mondayNoon));

        // Act
        _ = TestConsoleRunner.Run(_ => presenter.RenderPullRequestTable(reportData, createdOn.AddDays(-2)));

        // Assert
        formattedDurations.Should().HaveCount(2);
        formattedDurations.Should().OnlyContain(days => days == 1.0);
    }

    private static ReportParameters CreateReportParameters(DateTimeOffset filterDate, bool excludeWeekend = false)
    {
        return new ReportParameters(
            filterDate,
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            [],
            excludeWeekend);
    }
}

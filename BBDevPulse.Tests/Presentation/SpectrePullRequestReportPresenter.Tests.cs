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
        formatter.Setup(x => x.Format(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
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
        formatter.Verify(
            x => x.Format(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()),
            Times.Exactly(2));
    }

    private static ReportParameters CreateReportParameters(DateTimeOffset filterDate)
    {
        return new ReportParameters(
            filterDate,
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            []);
    }
}

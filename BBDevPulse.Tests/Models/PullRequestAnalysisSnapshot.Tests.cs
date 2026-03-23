using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PullRequestAnalysisSnapshotTests
{
    [Fact(DisplayName = "Constructor throws when activities are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenActivitiesAreNullThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<PullRequestActivity> activities = null!;

        // Act
        Action act = () => _ = new PullRequestAnalysisSnapshot(
            activities,
            [],
            PullRequestSizeSummary.Empty,
            []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when correction commits are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenCorrectionCommitsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<PullRequestCommitInfo> correctionCommits = null!;

        // Act
        Action act = () => _ = new PullRequestAnalysisSnapshot(
            [],
            correctionCommits,
            PullRequestSizeSummary.Empty,
            []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when commit activities are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenCommitActivitiesAreNullThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<DeveloperCommitActivity> commitActivities = null!;

        // Act
        Action act = () => _ = new PullRequestAnalysisSnapshot(
            [],
            [],
            PullRequestSizeSummary.Empty,
            commitActivities);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor sets all properties")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenArgumentsAreValidSetsProperties()
    {
        // Arrange
        var reviewer = new DeveloperIdentity(new UserUuid("{reviewer-1}"), new DisplayName("Reviewer"));
        var activity = new PullRequestActivity(
            new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
            null,
            reviewer,
            new ActivityComment(reviewer, new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero)),
            null);
        var commit = new PullRequestCommitInfo(
            "commit-1",
            new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
            "Fix cache mapping");
        var commitActivity = new DeveloperCommitActivity(
            "Repo",
            "repo",
            new PullRequestId(42),
            "commit-1",
            "Fix cache mapping",
            new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
            new PullRequestSizeSummary(1, 5, 2));

        // Act
        var snapshot = new PullRequestAnalysisSnapshot(
            [activity],
            [commit],
            new PullRequestSizeSummary(3, 25, 5),
            [commitActivity],
            hasEnrichment: true);

        // Assert
        snapshot.Activities.Should().ContainSingle().Which.Should().BeSameAs(activity);
        snapshot.CorrectionCommits.Should().ContainSingle().Which.Should().BeSameAs(commit);
        snapshot.SizeSummary.FilesChanged.Should().Be(3);
        snapshot.SizeSummary.LineChurn.Should().Be(30);
        snapshot.CommitActivities.Should().ContainSingle().Which.Should().BeSameAs(commitActivity);
        snapshot.HasEnrichment.Should().BeTrue();
    }
}

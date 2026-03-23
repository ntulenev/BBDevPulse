using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class CachedPullRequestAnalysisSnapshotTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Arrange
        var activities = new[] { new CachedPullRequestActivity() };
        var commits = new[] { new CachedPullRequestCommitInfo() };
        var sizeSummary = new CachedPullRequestSizeSummary { FilesChanged = 1, LinesAdded = 2, LinesRemoved = 3 };
        var commitActivities = new[] { new CachedDeveloperCommitActivity() };

        // Act
        var snapshot = new CachedPullRequestAnalysisSnapshot
        {
            Activities = activities,
            CorrectionCommits = commits,
            SizeSummary = sizeSummary,
            CommitActivities = commitActivities,
            HasEnrichment = true
        };

        // Assert
        snapshot.Activities.Should().BeSameAs(activities);
        snapshot.CorrectionCommits.Should().BeSameAs(commits);
        snapshot.SizeSummary.Should().BeSameAs(sizeSummary);
        snapshot.CommitActivities.Should().BeSameAs(commitActivities);
        snapshot.HasEnrichment.Should().BeTrue();
    }
}

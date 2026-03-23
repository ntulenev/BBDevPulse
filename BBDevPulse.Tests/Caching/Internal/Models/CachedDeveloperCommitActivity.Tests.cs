using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class CachedDeveloperCommitActivityTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero);
        var sizeSummary = new CachedPullRequestSizeSummary { FilesChanged = 1, LinesAdded = 5, LinesRemoved = 2 };

        // Act
        var activity = new CachedDeveloperCommitActivity
        {
            Repository = "Repo",
            RepositorySlug = "repo",
            PullRequestId = 42,
            CommitHash = "commit-1",
            Message = "Fix cache mapping",
            Date = date,
            SizeSummary = sizeSummary
        };

        // Assert
        activity.Repository.Should().Be("Repo");
        activity.RepositorySlug.Should().Be("repo");
        activity.PullRequestId.Should().Be(42);
        activity.CommitHash.Should().Be("commit-1");
        activity.Message.Should().Be("Fix cache mapping");
        activity.Date.Should().Be(date);
        activity.SizeSummary.Should().BeSameAs(sizeSummary);
    }
}

using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class CachedPullRequestCommitInfoTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero);

        // Act
        var commit = new CachedPullRequestCommitInfo
        {
            Hash = "commit-1",
            Date = date,
            Message = "Fix cache mapping"
        };

        // Assert
        commit.Hash.Should().Be("commit-1");
        commit.Date.Should().Be(date);
        commit.Message.Should().Be("Fix cache mapping");
    }
}

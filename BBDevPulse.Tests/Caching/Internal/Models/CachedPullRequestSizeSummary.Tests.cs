using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class CachedPullRequestSizeSummaryTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Act
        var summary = new CachedPullRequestSizeSummary
        {
            FilesChanged = 3,
            LinesAdded = 25,
            LinesRemoved = 5
        };

        // Assert
        summary.FilesChanged.Should().Be(3);
        summary.LinesAdded.Should().Be(25);
        summary.LinesRemoved.Should().Be(5);
    }
}

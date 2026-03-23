using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class PullRequestAnalysisCacheDocumentTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Arrange
        var snapshot = new CachedPullRequestAnalysisSnapshot();

        // Act
        var document = new PullRequestAnalysisCacheDocument
        {
            Version = 7,
            Snapshot = snapshot
        };

        // Assert
        document.Version.Should().Be(7);
        document.Snapshot.Should().BeSameAs(snapshot);
    }
}

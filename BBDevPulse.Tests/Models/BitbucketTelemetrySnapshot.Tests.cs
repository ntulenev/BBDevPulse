using BBDevPulse.Models;

using FluentAssertions;

namespace BBDevPulse.Tests.Models;

public sealed class BitbucketTelemetrySnapshotTests
{
    [Fact(DisplayName = "Properties return constructor values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenInitializedReturnConstructorValues()
    {
        // Arrange
        var statistics = new[]
        {
            new BitbucketApiRequestStatistic("GET /user", 2)
        };
        var snapshot = new BitbucketTelemetrySnapshot(true, 7, 3, 4, 5, 6, statistics);

        // Assert
        snapshot.IsEnabled.Should().BeTrue();
        snapshot.TotalRequests.Should().Be(7);
        snapshot.AnalysisSnapshotCacheHits.Should().Be(3);
        snapshot.AnalysisSnapshotCacheMisses.Should().Be(4);
        snapshot.AnalysisSnapshotCacheStores.Should().Be(5);
        snapshot.EstimatedAvoidedRequests.Should().Be(6);
        snapshot.RequestStatistics.Should().BeSameAs(statistics);
    }
}

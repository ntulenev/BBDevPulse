using BBDevPulse.Models;

using FluentAssertions;

namespace BBDevPulse.Tests.Models;

public sealed class BitbucketApiRequestStatisticTests
{
    [Fact(DisplayName = "Properties return constructor values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenInitializedReturnConstructorValues()
    {
        // Arrange
        var statistic = new BitbucketApiRequestStatistic("GET /user", 3);

        // Assert
        statistic.ApiName.Should().Be("GET /user");
        statistic.RequestCount.Should().Be(3);
    }
}

using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PullRequestSizeClassifierTests
{
    [Theory(DisplayName = "Classify maps churn ranges to expected T-shirt sizes")]
    [InlineData(0, "XS")]
    [InlineData(100, "XS")]
    [InlineData(101, "S")]
    [InlineData(300, "S")]
    [InlineData(301, "M")]
    [InlineData(700, "M")]
    [InlineData(701, "L")]
    [InlineData(1200, "L")]
    [InlineData(1201, "XL")]
    [Trait("Category", "Unit")]
    public void ClassifyWhenCalledReturnsExpectedLabel(int churn, string expected)
    {
        // Act
        var result = PullRequestSizeClassifier.Classify(churn);

        // Assert
        result.Should().Be(expected);
    }
}

using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PullRequestIdTests
{
    [Theory(DisplayName = "Constructor throws when value is not positive")]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueIsNotPositiveThrowsArgumentOutOfRangeException(int value)
    {
        // Act
        Action act = () => _ = new PullRequestId(value);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Constructor sets value and ToString returns invariant string")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueIsPositiveSetsValueAndToString()
    {
        // Arrange
        const int value = 42;

        // Act
        var id = new PullRequestId(value);

        // Assert
        id.Value.Should().Be(value);
        id.ToString().Should().Be("42");
    }
}

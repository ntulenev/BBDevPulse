using BBDevPulse.Configuration;

using FluentAssertions;

namespace BBDevPulse.Tests.Configuration;

public sealed class BitbucketTelemetryOptionsTests
{
    [Fact(DisplayName = "Enabled defaults to true")]
    [Trait("Category", "Unit")]
    public void EnabledWhenNotConfiguredDefaultsToTrue()
    {
        // Arrange
        var options = new BitbucketTelemetryOptions();

        // Assert
        options.Enabled.Should().BeTrue();
    }

    [Fact(DisplayName = "Enabled returns configured value")]
    [Trait("Category", "Unit")]
    public void EnabledWhenConfiguredReturnsAssignedValue()
    {
        // Arrange
        var options = new BitbucketTelemetryOptions
        {
            Enabled = false
        };

        // Assert
        options.Enabled.Should().BeFalse();
    }
}

using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class CachedDeveloperIdentityTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Act
        var identity = new CachedDeveloperIdentity
        {
            Uuid = "{reviewer-1}",
            DisplayName = "Reviewer"
        };

        // Assert
        identity.Uuid.Should().Be("{reviewer-1}");
        identity.DisplayName.Should().Be("Reviewer");
    }
}

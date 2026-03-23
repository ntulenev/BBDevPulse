using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class CachedActivityApprovalTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Arrange
        var user = new CachedDeveloperIdentity { Uuid = "{reviewer-1}", DisplayName = "Reviewer" };
        var date = new DateTimeOffset(2026, 3, 20, 11, 0, 0, TimeSpan.Zero);

        // Act
        var approval = new CachedActivityApproval
        {
            User = user,
            Date = date
        };

        // Assert
        approval.User.Should().BeSameAs(user);
        approval.Date.Should().Be(date);
    }
}

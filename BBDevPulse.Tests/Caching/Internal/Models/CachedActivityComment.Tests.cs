using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class CachedActivityCommentTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Arrange
        var user = new CachedDeveloperIdentity { Uuid = "{reviewer-1}", DisplayName = "Reviewer" };
        var date = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);

        // Act
        var comment = new CachedActivityComment
        {
            User = user,
            Date = date
        };

        // Assert
        comment.User.Should().BeSameAs(user);
        comment.Date.Should().Be(date);
    }
}

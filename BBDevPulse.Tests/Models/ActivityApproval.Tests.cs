using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class ActivityApprovalTests
{
    [Fact(DisplayName = "Constructor sets user and date")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValuesAreValidSetsProperties()
    {
        // Arrange
        var user = new DeveloperIdentity(
            new UserUuid("{ABC-123}"),
            new DisplayName("Alice"));
        var date = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);

        // Act
        var approval = new ActivityApproval(user, date);

        // Assert
        approval.User.Should().Be(user);
        approval.Date.Should().Be(date);
    }

    [Fact(DisplayName = "IsOnOrAfter returns true when approval date equals filter date")]
    [Trait("Category", "Unit")]
    public void IsOnOrAfterWhenDateEqualsFilterDateReturnsTrue()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        var approval = new ActivityApproval(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: date);

        // Act
        var result = approval.IsOnOrAfter(date);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "IsByDifferentDeveloper returns false when approval user matches pull request author")]
    [Trait("Category", "Unit")]
    public void IsByDifferentDeveloperWhenApprovalUserMatchesAuthorReturnsFalse()
    {
        // Arrange
        var approvalUser = new DeveloperIdentity(
            new UserUuid("{ABC-123}"),
            new DisplayName("Alice"));
        var authorIdentity = new DeveloperIdentity(
            new UserUuid("{abc-123}"),
            new DisplayName("Alice PR"));
        var approval = new ActivityApproval(
            user: approvalUser,
            date: new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero));

        // Act
        var result = approval.IsByDifferentDeveloper(authorIdentity);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "IsByDifferentDeveloper returns true when pull request author identity is missing")]
    [Trait("Category", "Unit")]
    public void IsByDifferentDeveloperWhenAuthorIdentityIsMissingReturnsTrue()
    {
        // Arrange
        var approval = new ActivityApproval(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero));

        // Act
        var result = approval.IsByDifferentDeveloper(authorIdentity: null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "TryUpdateFirstReaction updates first reaction when approval is earlier")]
    [Trait("Category", "Unit")]
    public void TryUpdateFirstReactionWhenApprovalDateIsEarlierUpdatesValue()
    {
        // Arrange
        var earlierApprovalDate = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset? firstReactionOn = earlierApprovalDate.AddMinutes(20);
        var approval = new ActivityApproval(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: earlierApprovalDate);

        // Act
        var result = approval.TryUpdateFirstReaction(ref firstReactionOn);

        // Assert
        result.Should().BeTrue();
        firstReactionOn.Should().Be(earlierApprovalDate);
    }

    [Fact(DisplayName = "TryUpdateFirstReaction sets first reaction when it is not initialized")]
    [Trait("Category", "Unit")]
    public void TryUpdateFirstReactionWhenFirstReactionIsNullSetsValue()
    {
        // Arrange
        DateTimeOffset? firstReactionOn = null;
        var approvalDate = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        var approval = new ActivityApproval(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: approvalDate);

        // Act
        var result = approval.TryUpdateFirstReaction(ref firstReactionOn);

        // Assert
        result.Should().BeTrue();
        firstReactionOn.Should().Be(approvalDate);
    }

    [Fact(DisplayName = "TryUpdateFirstReaction does not update when approval date is later")]
    [Trait("Category", "Unit")]
    public void TryUpdateFirstReactionWhenApprovalDateIsLaterDoesNotUpdateValue()
    {
        // Arrange
        var firstReactionDate = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset? firstReactionOn = firstReactionDate;
        var approval = new ActivityApproval(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: firstReactionDate.AddMinutes(10));

        // Act
        var result = approval.TryUpdateFirstReaction(ref firstReactionOn);

        // Assert
        result.Should().BeFalse();
        firstReactionOn.Should().Be(firstReactionDate);
    }
}

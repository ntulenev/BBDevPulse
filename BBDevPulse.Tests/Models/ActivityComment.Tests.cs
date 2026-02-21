using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class ActivityCommentTests
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
        var comment = new ActivityComment(user, date);

        // Assert
        comment.User.Should().Be(user);
        comment.Date.Should().Be(date);
    }

    [Fact(DisplayName = "IsOnOrAfter returns true when comment date equals filter date")]
    [Trait("Category", "Unit")]
    public void IsOnOrAfterWhenDateEqualsFilterDateReturnsTrue()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        var comment = new ActivityComment(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: date);

        // Act
        var result = comment.IsOnOrAfter(date);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "IsByDifferentDeveloper returns false when comment author and pull request author are the same developer")]
    [Trait("Category", "Unit")]
    public void IsByDifferentDeveloperWhenAuthorMatchesCommentUserReturnsFalse()
    {
        // Arrange
        var commentUser = new DeveloperIdentity(
            new UserUuid("{ABC-123}"),
            new DisplayName("Alice"));
        var authorIdentity = new DeveloperIdentity(
            new UserUuid("{abc-123}"),
            new DisplayName("Alice PR"));
        var comment = new ActivityComment(
            user: commentUser,
            date: new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero));

        // Act
        var result = comment.IsByDifferentDeveloper(authorIdentity);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "IsByDifferentDeveloper returns true when pull request author identity is missing")]
    [Trait("Category", "Unit")]
    public void IsByDifferentDeveloperWhenAuthorIdentityIsMissingReturnsTrue()
    {
        // Arrange
        var comment = new ActivityComment(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero));

        // Act
        var result = comment.IsByDifferentDeveloper(authorIdentity: null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "IsByDifferentDeveloper returns true when comment author and pull request author differ")]
    [Trait("Category", "Unit")]
    public void IsByDifferentDeveloperWhenAuthorDiffersReturnsTrue()
    {
        // Arrange
        var comment = new ActivityComment(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero));
        var authorIdentity = new DeveloperIdentity(
            new UserUuid("{DEF-999}"),
            new DisplayName("Bob"));

        // Act
        var result = comment.IsByDifferentDeveloper(authorIdentity);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "TryUpdateFirstReaction sets first reaction when it is not initialized")]
    [Trait("Category", "Unit")]
    public void TryUpdateFirstReactionWhenFirstReactionIsNullSetsValue()
    {
        // Arrange
        DateTimeOffset? firstReactionOn = null;
        var reactionDate = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        var comment = new ActivityComment(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: reactionDate);

        // Act
        var result = comment.TryUpdateFirstReaction(ref firstReactionOn);

        // Assert
        result.Should().BeTrue();
        firstReactionOn.Should().Be(reactionDate);
    }

    [Fact(DisplayName = "TryUpdateFirstReaction updates first reaction when comment date is earlier")]
    [Trait("Category", "Unit")]
    public void TryUpdateFirstReactionWhenCommentDateIsEarlierUpdatesValue()
    {
        // Arrange
        var earlierReactionDate = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset? firstReactionOn = earlierReactionDate.AddMinutes(15);
        var comment = new ActivityComment(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: earlierReactionDate);

        // Act
        var result = comment.TryUpdateFirstReaction(ref firstReactionOn);

        // Assert
        result.Should().BeTrue();
        firstReactionOn.Should().Be(earlierReactionDate);
    }

    [Fact(DisplayName = "TryUpdateFirstReaction does not update when comment date is later")]
    [Trait("Category", "Unit")]
    public void TryUpdateFirstReactionWhenCommentDateIsLaterDoesNotUpdateValue()
    {
        // Arrange
        var firstReactionDate = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset? firstReactionOn = firstReactionDate;
        var comment = new ActivityComment(
            user: new DeveloperIdentity(
                new UserUuid("{ABC-123}"),
                new DisplayName("Alice")),
            date: firstReactionDate.AddMinutes(15));

        // Act
        var result = comment.TryUpdateFirstReaction(ref firstReactionOn);

        // Assert
        result.Should().BeFalse();
        firstReactionOn.Should().Be(firstReactionDate);
    }
}

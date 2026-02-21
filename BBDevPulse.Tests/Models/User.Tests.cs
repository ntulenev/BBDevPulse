using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class UserTests
{
    [Fact(DisplayName = "Constructor throws when display name is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDisplayNameIsNullThrowsArgumentNullException()
    {
        // Arrange
        DisplayName displayName = null!;
        var uuid = new UserUuid("{uuid}");

        // Act
        Action act = () => _ = new User(displayName, uuid);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when UUID is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenUuidIsNullThrowsArgumentNullException()
    {
        // Arrange
        var displayName = new DisplayName("Alice");
        UserUuid uuid = null!;

        // Act
        Action act = () => _ = new User(displayName, uuid);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor sets properties")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValuesAreValidSetsProperties()
    {
        // Arrange
        var displayName = new DisplayName("Alice");
        var uuid = new UserUuid("{uuid}");

        // Act
        var user = new User(displayName, uuid);

        // Assert
        user.DisplayName.Should().Be(displayName);
        user.Uuid.Should().Be(uuid);
    }

    [Fact(DisplayName = "ToDeveloperIdentity maps user fields to identity")]
    [Trait("Category", "Unit")]
    public void ToDeveloperIdentityWhenCalledReturnsIdentityWithSameValues()
    {
        // Arrange
        var user = new User(new DisplayName("Alice"), new UserUuid("{uuid}"));

        // Act
        var identity = user.ToDeveloperIdentity();

        // Assert
        identity.DisplayName.Value.Should().Be("Alice");
        identity.Uuid!.Value.Should().Be("{uuid}");
    }
}

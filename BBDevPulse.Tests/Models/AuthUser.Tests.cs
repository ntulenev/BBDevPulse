using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class AuthUserTests
{
    [Fact(DisplayName = "Constructor throws when display name is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDisplayNameIsNullThrowsArgumentNullException()
    {
        // Arrange
        DisplayName displayName = null!;
        var username = new Username("alice");
        var uuid = new UserUuid("{uuid}");

        // Act
        Action act = () => _ = new AuthUser(displayName, username, uuid);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when username is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenUsernameIsNullThrowsArgumentNullException()
    {
        // Arrange
        var displayName = new DisplayName("Alice");
        Username username = null!;
        var uuid = new UserUuid("{uuid}");

        // Act
        Action act = () => _ = new AuthUser(displayName, username, uuid);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when UUID is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenUuidIsNullThrowsArgumentNullException()
    {
        // Arrange
        var displayName = new DisplayName("Alice");
        var username = new Username("alice");
        UserUuid uuid = null!;

        // Act
        Action act = () => _ = new AuthUser(displayName, username, uuid);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor sets properties")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValuesAreValidSetsProperties()
    {
        // Arrange
        var displayName = new DisplayName("Alice");
        var username = new Username("alice");
        var uuid = new UserUuid("{uuid}");

        // Act
        var user = new AuthUser(displayName, username, uuid);

        // Assert
        user.DisplayName.Should().Be(displayName);
        user.Username.Should().Be(username);
        user.Uuid.Should().Be(uuid);
    }
}

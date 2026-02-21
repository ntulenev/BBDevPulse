using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class DeveloperIdentityTests
{
    [Fact(DisplayName = "Constructor throws when display name is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDisplayNameIsNullThrowsArgumentNullException()
    {
        // Arrange
        DisplayName displayName = null!;

        // Act
        Action act = () => _ = new DeveloperIdentity(new UserUuid("{uuid}"), displayName);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "IsSameIdentity compares UUID values case-insensitively when both identities have UUIDs")]
    [Trait("Category", "Unit")]
    public void IsSameIdentityWhenBothHaveUuidsComparesUuidsIgnoringCase()
    {
        // Arrange
        var left = new DeveloperIdentity(new UserUuid("{ABC-1}"), new DisplayName("Alice"));
        var right = new DeveloperIdentity(new UserUuid("{abc-1}"), new DisplayName("Other Name"));

        // Act
        var result = left.IsSameIdentity(right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "IsSameIdentity falls back to display names when UUID is missing")]
    [Trait("Category", "Unit")]
    public void IsSameIdentityWhenUuidIsMissingComparesDisplayNameIgnoringCase()
    {
        // Arrange
        var left = new DeveloperIdentity(uuid: null, displayName: new DisplayName("Alice"));
        var right = new DeveloperIdentity(uuid: null, displayName: new DisplayName("ALICE"));

        // Act
        var result = left.IsSameIdentity(right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "ToKey returns UUID when UUID exists")]
    [Trait("Category", "Unit")]
    public void ToKeyWhenUuidExistsReturnsUuidValue()
    {
        // Arrange
        var identity = new DeveloperIdentity(new UserUuid("{uuid}"), new DisplayName("Alice"));

        // Act
        var key = identity.ToKey();

        // Assert
        key.Should().Be("{uuid}");
    }

    [Fact(DisplayName = "ToKey returns display name when UUID is missing")]
    [Trait("Category", "Unit")]
    public void ToKeyWhenUuidIsMissingReturnsDisplayName()
    {
        // Arrange
        var identity = new DeveloperIdentity(uuid: null, displayName: new DisplayName("Alice"));

        // Act
        var key = identity.ToKey();

        // Assert
        key.Should().Be("Alice");
    }
}

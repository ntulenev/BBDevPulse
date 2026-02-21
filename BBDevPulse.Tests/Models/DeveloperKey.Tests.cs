using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class DeveloperKeyTests
{
    [Fact(DisplayName = "UUID constructor throws when UUID is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenUuidIsNullThrowsArgumentNullException()
    {
        // Arrange
        UserUuid uuid = null!;

        // Act
        Action act = () => _ = new DeveloperKey(uuid);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Display name constructor throws when display name is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDisplayNameIsNullThrowsArgumentNullException()
    {
        // Arrange
        DisplayName displayName = null!;

        // Act
        Action act = () => _ = new DeveloperKey(displayName);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Keys built from UUIDs compare equal ignoring case")]
    [Trait("Category", "Unit")]
    public void EqualsWhenBothKeysUseUuidAndDifferOnlyByCaseReturnsTrue()
    {
        // Arrange
        var left = new DeveloperKey(new UserUuid("{ABC-123}"));
        var right = new DeveloperKey(new UserUuid("{abc-123}"));

        // Act
        var result = left.Equals(right);

        // Assert
        result.Should().BeTrue();
        (left == right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact(DisplayName = "Keys built from display names compare equal ignoring case when UUID is missing")]
    [Trait("Category", "Unit")]
    public void EqualsWhenBothKeysUseDisplayNameAndDifferOnlyByCaseReturnsTrue()
    {
        // Arrange
        var left = new DeveloperKey(new DisplayName("Alice"));
        var right = new DeveloperKey(new DisplayName("ALICE"));

        // Act
        var result = left.Equals(right);

        // Assert
        result.Should().BeTrue();
        (left == right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact(DisplayName = "Key with UUID is not equal to key with display name only")]
    [Trait("Category", "Unit")]
    public void EqualsWhenOneKeyUsesUuidAndOtherUsesDisplayNameReturnsFalse()
    {
        // Arrange
        var left = new DeveloperKey(new UserUuid("{ABC-123}"));
        var right = new DeveloperKey(new DisplayName("Alice"));

        // Act
        var result = left.Equals(right);

        // Assert
        result.Should().BeFalse();
        (left != right).Should().BeTrue();
    }

    [Fact(DisplayName = "ToString returns UUID when UUID exists")]
    [Trait("Category", "Unit")]
    public void ToStringWhenUuidExistsReturnsUuidValue()
    {
        // Arrange
        var key = new DeveloperKey(new UserUuid("{ABC-123}"));

        // Act
        var result = key.ToString();

        // Assert
        result.Should().Be("{ABC-123}");
    }

    [Fact(DisplayName = "ToString returns display name when UUID is missing")]
    [Trait("Category", "Unit")]
    public void ToStringWhenUuidIsMissingReturnsDisplayName()
    {
        // Arrange
        var key = new DeveloperKey(new DisplayName("Alice"));

        // Act
        var result = key.ToString();

        // Assert
        result.Should().Be("Alice");
    }

    [Fact(DisplayName = "FromIdentity prefers UUID when identity contains UUID")]
    [Trait("Category", "Unit")]
    public void FromIdentityWhenIdentityContainsUuidUsesUuidAsKey()
    {
        // Arrange
        var identity = new DeveloperIdentity(
            new UserUuid("{ABC-123}"),
            new DisplayName("Alice"));

        // Act
        var key = DeveloperKey.FromIdentity(identity);

        // Assert
        key.Uuid.Should().NotBeNull();
        key.Uuid!.Value.Should().Be("{ABC-123}");
        key.DisplayName.Should().BeNull();
    }

    [Fact(DisplayName = "FromIdentity falls back to display name when UUID is missing")]
    [Trait("Category", "Unit")]
    public void FromIdentityWhenIdentityHasNoUuidUsesDisplayNameAsKey()
    {
        // Arrange
        var identity = new DeveloperIdentity(
            uuid: null,
            displayName: new DisplayName("Alice"));

        // Act
        var key = DeveloperKey.FromIdentity(identity);

        // Assert
        key.Uuid.Should().BeNull();
        key.DisplayName.Should().NotBeNull();
        key.DisplayName!.Value.Should().Be("Alice");
    }

    [Fact(DisplayName = "Equals object overload returns true for equivalent key")]
    [Trait("Category", "Unit")]
    public void EqualsObjectWhenEquivalentKeyIsProvidedReturnsTrue()
    {
        // Arrange
        var key = new DeveloperKey(new UserUuid("{ABC-123}"));
        object other = new DeveloperKey(new UserUuid("{abc-123}"));

        // Act
        var result = key.Equals(other);

        // Assert
        result.Should().BeTrue();
    }
}

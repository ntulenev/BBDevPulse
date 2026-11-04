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

    [Fact(DisplayName = "Equals object overload returns false for non-key object")]
    [Trait("Category", "Unit")]
    public void EqualsObjectWhenObjectIsNotDeveloperKeyReturnsFalse()
    {
        // Arrange
        var key = new DeveloperKey(new UserUuid("{ABC-123}"));

        // Act
        var result = key.Equals(new object());

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "Default key hashes to zero and stringifies to empty string")]
    [Trait("Category", "Unit")]
    public void DefaultKeyWhenUsedReturnsZeroHashAndEmptyString()
    {
        // Arrange
        var key = default(DeveloperKey);

        // Act
        var hashCode = key.GetHashCode();
        var value = key.ToString();

        // Assert
        hashCode.Should().Be(0);
        value.Should().BeEmpty();
    }

    [Fact(DisplayName = "Default keys compare equal")]
    [Trait("Category", "Unit")]
    public void EqualsWhenBothKeysAreDefaultReturnsTrue()
    {
        // Arrange
        var left = default(DeveloperKey);
        var right = default(DeveloperKey);

        // Act
        var result = left.Equals(right);

        // Assert
        result.Should().BeTrue();
    }
}

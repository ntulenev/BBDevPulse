using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class DeveloperStatsTests
{
    [Fact(DisplayName = "Constructor throws when display name is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDisplayNameIsNullThrowsArgumentNullException()
    {
        // Arrange
        DisplayName displayName = null!;

        // Act
        Action act = () => _ = new DeveloperStats(displayName);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor sets display name and numeric counters default to zero")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDisplayNameIsValidSetsDisplayNameAndDefaultCounters()
    {
        // Arrange
        var displayName = new DisplayName("Alice");

        // Act
        var stats = new DeveloperStats(displayName);

        // Assert
        stats.DisplayName.Should().Be(displayName);
        stats.PrsOpenedSince.Should().Be(0);
        stats.PrsMergedAfter.Should().Be(0);
        stats.CommentsAfter.Should().Be(0);
        stats.ApprovalsAfter.Should().Be(0);
        stats.Corrections.Should().Be(0);
        stats.Grade.Should().Be(DeveloperStats.NotAvailable);
        stats.Department.Should().Be(DeveloperStats.NotAvailable);
    }

    [Fact(DisplayName = "Properties can be updated")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenUpdatedStoreNewValues()
    {
        // Arrange
        var stats = new DeveloperStats(new DisplayName("Alice"));

        // Act
        stats.DisplayName = new DisplayName("Alice Updated");
        stats.PrsOpenedSince = 1;
        stats.PrsMergedAfter = 2;
        stats.CommentsAfter = 3;
        stats.ApprovalsAfter = 4;
        stats.Corrections = 5;
        stats.Grade = "Senior";
        stats.Department = "Platform";

        // Assert
        stats.DisplayName.Value.Should().Be("Alice Updated");
        stats.PrsOpenedSince.Should().Be(1);
        stats.PrsMergedAfter.Should().Be(2);
        stats.CommentsAfter.Should().Be(3);
        stats.ApprovalsAfter.Should().Be(4);
        stats.Corrections.Should().Be(5);
        stats.Grade.Should().Be("Senior");
        stats.Department.Should().Be("Platform");
    }
}

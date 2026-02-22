using FluentAssertions;

namespace BBDevPulse.Tests.Math;

public sealed class WorkDurationCalculatorTests
{
    [Fact(DisplayName = "Calculate returns zero when end is not after start")]
    [Trait("Category", "Unit")]
    public void CalculateWhenEndIsNotAfterStartReturnsZero()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var end = start;

        // Act
        var duration = BBDevPulse.Math.WorkDurationCalculator.Calculate(start, end, excludeWeekend: true);

        // Assert
        duration.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "Calculate returns full duration when weekend exclusion is disabled")]
    [Trait("Category", "Unit")]
    public void CalculateWhenExcludeWeekendIsFalseReturnsFullDuration()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero); // Friday
        var end = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero); // Monday

        // Act
        var duration = BBDevPulse.Math.WorkDurationCalculator.Calculate(start, end, excludeWeekend: false);

        // Assert
        duration.Should().Be(TimeSpan.FromDays(3));
    }

    [Fact(DisplayName = "Calculate skips Saturday and Sunday when weekend exclusion is enabled")]
    [Trait("Category", "Unit")]
    public void CalculateWhenExcludeWeekendIsTrueSkipsWeekendTime()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero); // Friday
        var end = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero); // Monday

        // Act
        var duration = BBDevPulse.Math.WorkDurationCalculator.Calculate(start, end, excludeWeekend: true);

        // Assert
        duration.Should().Be(TimeSpan.FromDays(1));
    }

    [Fact(DisplayName = "Calculate returns zero when interval is fully on weekend and exclusion is enabled")]
    [Trait("Category", "Unit")]
    public void CalculateWhenWeekendOnlyWithExcludeWeekendReturnsZero()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero); // Saturday
        var end = new DateTimeOffset(2026, 2, 22, 15, 0, 0, TimeSpan.Zero); // Sunday

        // Act
        var duration = BBDevPulse.Math.WorkDurationCalculator.Calculate(start, end, excludeWeekend: true);

        // Assert
        duration.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "Calculate skips configured excluded days when provided")]
    [Trait("Category", "Unit")]
    public void CalculateWhenExcludedDaysProvidedSkipsThoseDays()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 2, 2, 10, 0, 0, TimeSpan.Zero); // Monday
        var end = new DateTimeOffset(2026, 2, 4, 10, 0, 0, TimeSpan.Zero); // Wednesday
        IReadOnlySet<DateOnly> excludedDays = new HashSet<DateOnly> { new(2026, 2, 3) }; // Tuesday

        // Act
        var duration = BBDevPulse.Math.WorkDurationCalculator.Calculate(start, end, excludeWeekend: false, excludedDays);

        // Assert
        duration.Should().Be(TimeSpan.FromDays(1));
    }

    [Fact(DisplayName = "Calculate skips both weekends and excluded days when both are configured")]
    [Trait("Category", "Unit")]
    public void CalculateWhenWeekendAndExcludedDaysConfiguredSkipsBoth()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 2, 6, 10, 0, 0, TimeSpan.Zero); // Friday
        var end = new DateTimeOffset(2026, 2, 10, 10, 0, 0, TimeSpan.Zero); // Tuesday
        IReadOnlySet<DateOnly> excludedDays = new HashSet<DateOnly> { new(2026, 2, 9) }; // Monday

        // Act
        var duration = BBDevPulse.Math.WorkDurationCalculator.Calculate(start, end, excludeWeekend: true, excludedDays);

        // Assert
        duration.Should().Be(TimeSpan.FromHours(24));
    }
}

using FluentAssertions;

using BBDevPulse.Presentation.Formatters;

namespace BBDevPulse.Tests.Presentation.Formatters;

public sealed class DateDiffFormatterTests
{
    [Fact(DisplayName = "Format returns less than one hour token when period is under one hour")]
    [Trait("Category", "Unit")]
    public void FormatWhenPeriodIsUnderOneHourReturnsLessThanOneHourToken()
    {
        // Arrange
        var formatter = new DateDiffFormatter();
        var startPeriod = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        var endPeriod = startPeriod.AddMinutes(59);

        // Act
        var result = formatter.Format(startPeriod, endPeriod);

        // Assert
        result.Should().Be("<1h");
    }

    [Fact(DisplayName = "Format returns hours when period is at least one hour and less than one day")]
    [Trait("Category", "Unit")]
    public void FormatWhenPeriodIsAtLeastOneHourAndLessThanOneDayReturnsHours()
    {
        // Arrange
        var formatter = new DateDiffFormatter();
        var startPeriod = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        var endPeriod = startPeriod.AddHours(2.5);

        // Act
        var result = formatter.Format(startPeriod, endPeriod);

        // Assert
        result.Should().Be("2.5 hours");
    }

    [Fact(DisplayName = "Format returns days with hours in parentheses when period is one day or longer")]
    [Trait("Category", "Unit")]
    public void FormatWhenPeriodIsOneDayOrLongerReturnsDaysWithHoursInParentheses()
    {
        // Arrange
        var formatter = new DateDiffFormatter();
        var startPeriod = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        var endPeriod = startPeriod.AddHours(36);

        // Act
        var result = formatter.Format(startPeriod, endPeriod);

        // Assert
        result.Should().Be("1.5 days (36.0 hours)");
    }
}

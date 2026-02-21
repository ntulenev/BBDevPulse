using FluentAssertions;

namespace BBDevPulse.Tests.Math;

public sealed class StatisticsCalculatorTests
{
    [Fact(DisplayName = "Percentile throws when sorted values are null")]
    [Trait("Category", "Unit")]
    public void PercentileWhenSortedValuesAreNullThrowsArgumentNullException()
    {
        // Arrange
        var calculator = new BBDevPulse.Math.StatisticsCalculator();
        IReadOnlyList<double> sortedValues = null!;

        // Act
        Action act = () => _ = calculator.Percentile(sortedValues, 50);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Percentile returns zero when sorted values are empty")]
    [Trait("Category", "Unit")]
    public void PercentileWhenSortedValuesAreEmptyReturnsZero()
    {
        // Arrange
        var calculator = new BBDevPulse.Math.StatisticsCalculator();

        // Act
        var result = calculator.Percentile([], 50);

        // Assert
        result.Should().Be(0);
    }

    [Fact(DisplayName = "Percentile returns exact value when position falls on an index")]
    [Trait("Category", "Unit")]
    public void PercentileWhenPositionFallsOnAnIndexReturnsExactValue()
    {
        // Arrange
        var calculator = new BBDevPulse.Math.StatisticsCalculator();
        var sortedValues = new List<double> { 10, 20, 30 };

        // Act
        var result = calculator.Percentile(sortedValues, 50);

        // Assert
        result.Should().Be(20);
    }

    [Fact(DisplayName = "Percentile interpolates between neighboring values")]
    [Trait("Category", "Unit")]
    public void PercentileWhenPositionFallsBetweenIndicesInterpolates()
    {
        // Arrange
        var calculator = new BBDevPulse.Math.StatisticsCalculator();
        var sortedValues = new List<double> { 10, 20, 30, 40 };

        // Act
        var result = calculator.Percentile(sortedValues, 75);

        // Assert
        result.Should().Be(32.5);
    }
}

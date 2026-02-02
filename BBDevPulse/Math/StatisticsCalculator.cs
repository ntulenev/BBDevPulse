using BBDevPulse.Abstractions;

namespace BBDevPulse.Math;

/// <summary>
/// Default implementation of <see cref="IStatisticsCalculator"/>.
/// </summary>
public sealed class StatisticsCalculator : IStatisticsCalculator
{
    /// <inheritdoc />
    public double Percentile(IReadOnlyList<double> sortedValues, int percentile)
    {
        ArgumentNullException.ThrowIfNull(sortedValues);
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var position = percentile / 100.0 * (sortedValues.Count - 1);
        var lowerIndex = (int)System.Math.Floor(position);
        var upperIndex = (int)System.Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var weight = position - lowerIndex;
        return sortedValues[lowerIndex] + (weight * (sortedValues[upperIndex] - sortedValues[lowerIndex]));
    }
}

namespace BBDevPulse.Abstractions;

/// <summary>
/// Provides statistical calculations for reporting.
/// </summary>
public interface IStatisticsCalculator
{
    /// <summary>
    /// Computes the percentile for the sorted values.
    /// </summary>
    /// <param name="sortedValues">Values sorted in ascending order.</param>
    /// <param name="percentile">Percentile to compute.</param>
    /// <returns>Percentile value.</returns>
    double Percentile(IReadOnlyList<double> sortedValues, int percentile);
}

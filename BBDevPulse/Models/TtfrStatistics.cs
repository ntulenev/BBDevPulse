namespace BBDevPulse.Models;

/// <summary>Shared time-to-first-review observations and numerical summary, in days.</summary>
public sealed record TtfrStatistics(IReadOnlyList<double> OrderedDays, double MedianDays, double P75Days)
{
    /// <summary>Gets the ordered, nonnegative durations copied from the supplied observations.</summary>
    public IReadOnlyList<double> OrderedDays { get; } = CopyObservations(OrderedDays);

    /// <summary>Median duration in days, or zero for an empty sample.</summary>
    public double MedianDays { get; } = ValidateDuration(MedianDays, nameof(MedianDays));

    /// <summary>75th percentile duration in days, or zero for an empty sample.</summary>
    public double P75Days { get; } = ValidateDuration(P75Days, nameof(P75Days));

    private static double ValidateDuration(double value, string parameter)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameter);
        }

        return value;
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<double> CopyObservations(IReadOnlyList<double> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var copy = observations.ToArray();
        for (var index = 0; index < copy.Length; index++)
        {
            if (!double.IsFinite(copy[index]) || copy[index] < 0 ||
                (index > 0 && copy[index] < copy[index - 1]))
            {
                throw new ArgumentException("Expected finite, ascending durations in days.", nameof(observations));
            }
        }

        return Array.AsReadOnly(copy);
    }

    /// <summary>Gets the number of observed pull requests with a first reaction.</summary>
    public int Count => OrderedDays.Count;
}

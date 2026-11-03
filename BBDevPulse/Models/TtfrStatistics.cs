namespace BBDevPulse.Models;

/// <summary>Shared time-to-first-review observations and numerical summary, in days.</summary>
public sealed record TtfrStatistics(IReadOnlyList<double> OrderedDays, double MedianDays, double P75Days)
{
    /// <summary>Gets the ordered, nonnegative durations copied from the supplied observations.</summary>
    public IReadOnlyList<double> OrderedDays { get; } = CopyObservations(OrderedDays);

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

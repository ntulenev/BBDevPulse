namespace BBDevPulse.Models;

/// <summary>Shared time-to-first-review observations and numerical summary, in days.</summary>
public sealed record TtfrStatistics(IReadOnlyList<double> OrderedDays, double MedianDays, double P75Days)
{
    /// <summary>Gets the number of observed pull requests with a first reaction.</summary>
    public int Count => OrderedDays.Count;
}

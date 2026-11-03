namespace BBDevPulse.Models;

/// <summary>Shared time-to-first-review observations and numerical summary, in days.</summary>
public sealed record TtfrStatistics(IReadOnlyList<double> OrderedDays, double MedianDays, double P75Days)
{
    public int Count => OrderedDays.Count;
}

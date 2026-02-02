namespace BBDevPulse.Abstractions;

/// <summary>
/// Formats date differences for reporting.
/// </summary>
public interface IDateDiffFormatter
{
    /// <summary>
    /// Formats the elapsed time between two dates.
    /// </summary>
    /// <param name="startPeriod">Start date.</param>
    /// <param name="endPeriod">End date.</param>
    /// <returns>Formatted duration string.</returns>
    string Format(DateTimeOffset startPeriod, DateTimeOffset endPeriod);
}

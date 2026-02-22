namespace BBDevPulse.Math;

/// <summary>
/// Calculates elapsed time with optional weekend exclusion.
/// </summary>
internal static class WorkDurationCalculator
{
    /// <summary>
    /// Calculates elapsed time between two timestamps.
    /// </summary>
    /// <param name="start">Start timestamp.</param>
    /// <param name="end">End timestamp.</param>
    /// <param name="excludeWeekend">Whether to exclude Saturday and Sunday.</param>
    /// <returns>Calculated duration.</returns>
    public static TimeSpan Calculate(DateTimeOffset start, DateTimeOffset end, bool excludeWeekend)
    {
        if (end <= start)
        {
            return TimeSpan.Zero;
        }

        if (!excludeWeekend)
        {
            return end - start;
        }

        var total = TimeSpan.Zero;
        var cursor = start;

        while (cursor < end)
        {
            var nextDay = new DateTimeOffset(cursor.Date.AddDays(1), cursor.Offset);
            var segmentEnd = end < nextDay ? end : nextDay;

            var isWeekend = cursor.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (!isWeekend)
            {
                total += segmentEnd - cursor;
            }

            cursor = segmentEnd;
        }

        return total < TimeSpan.Zero ? TimeSpan.Zero : total;
    }
}

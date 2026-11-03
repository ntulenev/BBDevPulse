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
    /// <param name="excludedDays">Optional days that should be excluded fully.</param>
    /// <param name="forcedIncludeDates">Dates that count as working days even when otherwise excluded.</param>
    /// <returns>Calculated duration.</returns>
    public static TimeSpan Calculate(
        DateTimeOffset start,
        DateTimeOffset end,
        bool excludeWeekend,
        IReadOnlySet<DateOnly>? excludedDays = null,
        IReadOnlySet<DateOnly>? forcedIncludeDates = null)
    {
        if (end <= start)
        {
            return TimeSpan.Zero;
        }

        var hasExcludedDays = excludedDays is { Count: > 0 };
        if (!excludeWeekend && !hasExcludedDays)
        {
            return end - start;
        }

        var total = TimeSpan.Zero;
        var cursor = start;

        while (cursor < end)
        {
            var nextDay = new DateTimeOffset(cursor.Date.AddDays(1), cursor.Offset);
            var segmentEnd = end < nextDay ? end : nextDay;

            var date = DateOnly.FromDateTime(cursor.Date);
            var isWeekend = cursor.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var isExcluded = hasExcludedDays && excludedDays!.Contains(date);
            if (forcedIncludeDates?.Contains(date) == true || ((!excludeWeekend || !isWeekend) && !isExcluded))
            {
                total += segmentEnd - cursor;
            }

            cursor = segmentEnd;
        }

        return total < TimeSpan.Zero ? TimeSpan.Zero : total;
    }
}

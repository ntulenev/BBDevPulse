using BBDevPulse.Abstractions;

namespace BBDevPulse.Presentation.Formatters;

/// <summary>
/// Formats date differences using report-friendly thresholds.
/// </summary>
public sealed class DateDiffFormatter : IDateDiffFormatter
{
    /// <inheritdoc />
    public string Format(DateTimeOffset startPeriod, DateTimeOffset endPeriod)
    {
        var days = (endPeriod - startPeriod).TotalDays;
        if (days < 1)
        {
            var hours = days * 24;
            return hours < 1 ? "<1h" : $"{hours:0.0} hours";
        }

        return $"{days:0.0} days";
    }
}

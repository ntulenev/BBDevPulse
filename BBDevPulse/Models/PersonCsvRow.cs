namespace BBDevPulse.Models;

/// <summary>
/// Represents a CSV row with developer metadata.
/// </summary>
/// <param name="Grade">Developer grade.</param>
/// <param name="Department">Developer department.</param>
public readonly record struct PersonCsvRow(string Grade, string Department);

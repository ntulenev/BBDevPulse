namespace BBDevPulse.Models;

/// <summary>
/// Represents a CSV row with developer metadata.
/// </summary>
/// <param name="Grade">Developer grade.</param>
/// <param name="Department">Developer department.</param>
public readonly record struct PersonCsvRow(string Grade, string Department)
{
    /// <summary>
    /// Determines whether the row belongs to the provided team.
    /// </summary>
    /// <param name="teamFilter">Team filter.</param>
    /// <returns>True when the row belongs to the requested team.</returns>
    public bool IsInTeam(string teamFilter) =>
        !string.IsNullOrWhiteSpace(teamFilter) &&
        !string.Equals(Department, DeveloperStats.NOT_AVAILABLE, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Department, teamFilter, StringComparison.OrdinalIgnoreCase);
}

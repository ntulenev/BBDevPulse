namespace BBDevPulse.Models;

/// <summary>
/// Represents a CSV row with developer metadata.
/// </summary>
/// <param name="Grade">Developer grade.</param>
/// <param name="Department">Developer department.</param>
/// <param name="Uuid">Optional Bitbucket UUID.</param>
public readonly record struct PersonCsvRow(string Grade, string Department, string? Uuid = null)
{
    /// <summary>Additional CSV rows with the same name.</summary>
    public IReadOnlyList<PersonCsvRow>? AdditionalRows { get; init; }

    /// <summary>Enumerates all CSV rows for this name.</summary>
    public IEnumerable<PersonCsvRow> Rows => new[] { this }.Concat(AdditionalRows ?? []);

    /// <summary>Compares Bitbucket and CSV UUIDs, allowing optional braces.</summary>
    public bool MatchesUuid(UserUuid? uuid) => uuid is not null && !string.IsNullOrWhiteSpace(Uuid) &&
        string.Equals(Uuid.Trim().Trim('{', '}'), uuid.Value.Trim().Trim('{', '}'), StringComparison.OrdinalIgnoreCase);

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

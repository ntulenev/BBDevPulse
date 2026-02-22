using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Loads developer metadata from configured CSV source.
/// </summary>
public interface IPeopleCsvProvider
{
    /// <summary>
    /// Gets people metadata keyed by developer display name.
    /// </summary>
    /// <returns>Dictionary with metadata by display name.</returns>
    Dictionary<DisplayName, PersonCsvRow> GetPeopleByDisplayName();
}

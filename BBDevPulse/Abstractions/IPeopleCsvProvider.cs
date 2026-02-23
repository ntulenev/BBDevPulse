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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary with metadata by display name.</returns>
    Task<Dictionary<DisplayName, PersonCsvRow>> GetPeopleByDisplayNameAsync(CancellationToken cancellationToken = default);
}

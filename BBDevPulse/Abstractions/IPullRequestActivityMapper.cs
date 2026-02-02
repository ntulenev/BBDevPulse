using System.Text.Json;

using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Maps Bitbucket activity JSON payloads into domain models.
/// </summary>
public interface IPullRequestActivityMapper
{
    /// <summary>
    /// Converts the raw activity payload into a domain model.
    /// </summary>
    /// <param name="activity">Raw activity payload.</param>
    /// <returns>Mapped activity model.</returns>
    PullRequestActivity Map(JsonElement activity);
}

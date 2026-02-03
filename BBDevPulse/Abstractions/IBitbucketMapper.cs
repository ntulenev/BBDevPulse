using BBDevPulse.Models;
using BBDevPulse.Transport;
using System.Text.Json;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Maps Bitbucket DTOs into domain models.
/// </summary>
internal interface IBitbucketMapper
{
    /// <summary>
    /// Maps authenticated user payload.
    /// </summary>
    AuthUser Map(AuthUserDto dto);

    /// <summary>
    /// Maps repository payload.
    /// </summary>
    Repository Map(RepositoryDto dto);

    /// <summary>
    /// Maps pull request payload.
    /// </summary>
    PullRequest Map(PullRequestDto dto);

    /// <summary>
    /// Maps pull request activity payload.
    /// </summary>
    PullRequestActivity Map(JsonElement activity);
}

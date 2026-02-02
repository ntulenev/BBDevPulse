using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

/// <summary>
/// Repository DTO from Bitbucket API.
/// </summary>
internal sealed class RepositoryDto
{
    /// <summary>
    /// Repository name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Repository slug.
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }
}

/// <summary>
/// Authenticated user DTO from Bitbucket API.
/// </summary>
internal sealed class AuthUserDto
{
    /// <summary>
    /// Display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Username.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// User UUID.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }
}

/// <summary>
/// Generic paginated response container.
/// </summary>
internal sealed class PaginatedResponse<T>
{
    /// <summary>
    /// Items on the current page.
    /// </summary>
    [JsonPropertyName("values")]
    public List<T> Values { get; init; } = [];

    /// <summary>
    /// URL of the next page.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }
}

/// <summary>
/// Pull request DTO from Bitbucket API.
/// </summary>
internal sealed class PullRequestDto
{
    /// <summary>
    /// Pull request identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// Pull request state.
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    /// <summary>
    /// Closed timestamp.
    /// </summary>
    [JsonPropertyName("closed_on")]
    public DateTimeOffset? ClosedOn { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    [JsonPropertyName("created_on")]
    public DateTimeOffset CreatedOn { get; init; }

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    [JsonPropertyName("updated_on")]
    public DateTimeOffset? UpdatedOn { get; init; }

    /// <summary>
    /// Merge timestamp.
    /// </summary>
    [JsonPropertyName("merged_on")]
    public DateTimeOffset? MergedOn { get; init; }

    /// <summary>
    /// Pull request author.
    /// </summary>
    [JsonPropertyName("author")]
    public UserDto? Author { get; init; }

    /// <summary>
    /// Destination metadata.
    /// </summary>
    [JsonPropertyName("destination")]
    public PullRequestDestinationDto? Destination { get; init; }
}

/// <summary>
/// Pull request destination DTO.
/// </summary>
internal sealed class PullRequestDestinationDto
{
    /// <summary>
    /// Destination branch.
    /// </summary>
    [JsonPropertyName("branch")]
    public PullRequestBranchDto? Branch { get; init; }
}

/// <summary>
/// Pull request branch DTO.
/// </summary>
internal sealed class PullRequestBranchDto
{
    /// <summary>
    /// Branch name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>
/// User DTO from Bitbucket API.
/// </summary>
internal sealed class UserDto
{
    /// <summary>
    /// Display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// User UUID.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }
}

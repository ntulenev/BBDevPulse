using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

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

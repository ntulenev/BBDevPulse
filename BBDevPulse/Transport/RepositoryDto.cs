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

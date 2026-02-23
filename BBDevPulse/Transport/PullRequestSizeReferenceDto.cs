using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

/// <summary>
/// Pull request DTO shape required for diffstat fallback.
/// </summary>
internal sealed class PullRequestSizeReferenceDto
{
    /// <summary>
    /// Source metadata.
    /// </summary>
    [JsonPropertyName("source")]
    public PullRequestEndpointDto? Source { get; init; }

    /// <summary>
    /// Destination metadata.
    /// </summary>
    [JsonPropertyName("destination")]
    public PullRequestEndpointDto? Destination { get; init; }
}

/// <summary>
/// Pull request endpoint metadata.
/// </summary>
internal sealed class PullRequestEndpointDto
{
    /// <summary>
    /// Endpoint commit metadata.
    /// </summary>
    [JsonPropertyName("commit")]
    public PullRequestCommitHashDto? Commit { get; init; }
}

/// <summary>
/// Pull request commit hash DTO.
/// </summary>
internal sealed class PullRequestCommitHashDto
{
    /// <summary>
    /// Commit hash.
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; init; }
}

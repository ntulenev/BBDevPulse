using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

/// <summary>
/// Pull request commit DTO from Bitbucket API.
/// </summary>
internal sealed class PullRequestCommitDto
{
    /// <summary>
    /// Commit hash.
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; init; }

    /// <summary>
    /// Commit timestamp.
    /// </summary>
    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; init; }
}

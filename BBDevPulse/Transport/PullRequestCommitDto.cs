using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

/// <summary>
/// Pull request commit DTO from Bitbucket API.
/// </summary>
internal sealed class PullRequestCommitDto
{
    /// <summary>
    /// Commit timestamp.
    /// </summary>
    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; init; }
}

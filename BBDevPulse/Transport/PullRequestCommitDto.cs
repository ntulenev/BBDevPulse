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

    /// <summary>
    /// Commit message summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public CommitSummaryDto? Summary { get; init; }

    internal sealed class CommitSummaryDto
    {
        /// <summary>
        /// Raw summary text.
        /// </summary>
        [JsonPropertyName("raw")]
        public string? Raw { get; init; }
    }
}

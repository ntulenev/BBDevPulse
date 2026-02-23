using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

/// <summary>
/// Pull request diffstat entry DTO.
/// </summary>
internal sealed class PullRequestDiffStatDto
{
    /// <summary>
    /// Added lines for the file entry.
    /// </summary>
    [JsonPropertyName("lines_added")]
    public int? LinesAdded { get; init; }

    /// <summary>
    /// Removed lines for the file entry.
    /// </summary>
    [JsonPropertyName("lines_removed")]
    public int? LinesRemoved { get; init; }
}

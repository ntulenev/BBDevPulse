using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

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

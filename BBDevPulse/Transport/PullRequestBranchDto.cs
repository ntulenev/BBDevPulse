using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

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

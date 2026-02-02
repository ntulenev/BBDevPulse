using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

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

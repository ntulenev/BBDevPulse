using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

/// <summary>
/// Authenticated user DTO from Bitbucket API.
/// </summary>
internal sealed class AuthUserDto
{
    /// <summary>
    /// Display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Username.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// User UUID.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }
}

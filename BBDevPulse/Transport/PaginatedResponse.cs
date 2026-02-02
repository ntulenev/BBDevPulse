using System.Text.Json.Serialization;

namespace BBDevPulse.Transport;

/// <summary>
/// Generic paginated response container.
/// </summary>
internal sealed class PaginatedResponse<T>
{
    /// <summary>
    /// Items on the current page.
    /// </summary>
    [JsonPropertyName("values")]
    public List<T> Values { get; init; } = [];

    /// <summary>
    /// URL of the next page.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }
}

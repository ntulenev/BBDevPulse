namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized developer identity.
/// </summary>
internal sealed class CachedDeveloperIdentity
{
    public string? Uuid { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}

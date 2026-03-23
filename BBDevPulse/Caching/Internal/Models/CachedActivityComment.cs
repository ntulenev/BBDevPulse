namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized comment activity.
/// </summary>
internal sealed class CachedActivityComment
{
    public CachedDeveloperIdentity User { get; init; } = null!;

    public DateTimeOffset Date { get; init; }
}

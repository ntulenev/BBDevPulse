namespace BBDevPulse.Caching.Internal.Models;

/// <summary>
/// Serialized approval activity.
/// </summary>
internal sealed class CachedActivityApproval
{
    public CachedDeveloperIdentity User { get; init; } = null!;

    public DateTimeOffset Date { get; init; }
}

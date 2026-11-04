namespace BBDevPulse.Models;

/// <summary>Duplicate name and its impact on the selected team filter.</summary>
/// <param name="Name">Shared display name.</param>
/// <param name="Uuids">Observed Bitbucket UUIDs.</param>
/// <param name="IsError">Whether the selected team contains an unresolved name.</param>
/// <param name="Message">Explanation of the impact.</param>
public sealed record IdentityCollisionNotice(string Name, IReadOnlyList<string> Uuids, bool IsError, string Message);

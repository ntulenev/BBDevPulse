namespace BBDevPulse.Models;

/// <summary>
/// Uniquely identifies a developer.
/// </summary>
public readonly struct DeveloperIdentity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperIdentity"/> struct.
    /// </summary>
    /// <param name="uuid">Bitbucket user UUID.</param>
    /// <param name="displayName">Display name for reporting.</param>
    public DeveloperIdentity(UserUuid? uuid, DisplayName displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        Uuid = uuid;
        DisplayName = displayName;
    }

    /// <summary>
    /// Bitbucket user UUID.
    /// </summary>
    public UserUuid? Uuid { get; }

    /// <summary>
    /// Display name for reporting.
    /// </summary>
    public DisplayName DisplayName { get; }

    /// <summary>
    /// Determines whether this identity refers to the same developer as another identity.
    /// </summary>
    /// <param name="other">Other identity.</param>
    /// <returns>True when identities match.</returns>
    public bool IsSameIdentity(DeveloperIdentity other)
    {
        if (Uuid is not null && other.Uuid is not null)
        {
            return string.Equals(Uuid.Value, other.Uuid.Value, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(DisplayName.Value, other.DisplayName.Value, StringComparison.OrdinalIgnoreCase);
    }
}

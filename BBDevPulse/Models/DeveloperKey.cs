namespace BBDevPulse.Models;

/// <summary>
/// Represents the preferred identity key used to aggregate developer stats.
/// </summary>
public readonly struct DeveloperKey : IEquatable<DeveloperKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperKey"/> struct using a UUID.
    /// </summary>
    /// <param name="uuid">Developer UUID.</param>
    public DeveloperKey(UserUuid uuid)
    {
        ArgumentNullException.ThrowIfNull(uuid);
        Uuid = uuid;
        DisplayName = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperKey"/> struct using a display name.
    /// </summary>
    /// <param name="displayName">Developer display name.</param>
    public DeveloperKey(DisplayName displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        Uuid = null;
        DisplayName = displayName;
    }

    /// <summary>
    /// UUID identity, if available.
    /// </summary>
    public UserUuid? Uuid { get; }

    /// <summary>
    /// Display name identity, if UUID is not available.
    /// </summary>
    public DisplayName? DisplayName { get; }

    /// <summary>
    /// Creates a <see cref="DeveloperKey"/> from the provided identity.
    /// </summary>
    /// <param name="identity">Developer identity.</param>
    /// <returns>Developer key.</returns>
    public static DeveloperKey FromIdentity(DeveloperIdentity identity)
    {
        return identity.Uuid is not null
            ? new DeveloperKey(identity.Uuid)
            : new DeveloperKey(identity.DisplayName);
    }

    /// <inheritdoc />
    public bool Equals(DeveloperKey other)
    {
        if (Uuid is not null && other.Uuid is not null)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Uuid.Value, other.Uuid.Value);
        }

        if (Uuid is null && other.Uuid is null)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(DisplayName?.Value, other.DisplayName?.Value);
        }

        return false;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DeveloperKey other && Equals(other);

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(DeveloperKey left, DeveloperKey right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(DeveloperKey left, DeveloperKey right) => !left.Equals(right);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (Uuid is not null)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Uuid.Value);
        }

        return DisplayName is null
            ? 0
            : StringComparer.OrdinalIgnoreCase.GetHashCode(DisplayName.Value);
    }

    /// <inheritdoc />
    public override string ToString() => Uuid?.Value ?? DisplayName?.Value ?? string.Empty;
}

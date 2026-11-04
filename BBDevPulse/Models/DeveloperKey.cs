namespace BBDevPulse.Models;

/// <summary>
/// Represents the preferred identity key used to aggregate developer stats.
/// The default value is empty, compares equal to other empty keys and hashes to zero.
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
    }

    /// <summary>UUID identity, if this is not the default key.</summary>
    public UserUuid? Uuid { get; }

    /// <summary>
    /// Creates a <see cref="DeveloperKey"/> from the provided identity.
    /// </summary>
    /// <param name="identity">Developer identity.</param>
    /// <returns>Developer key.</returns>
    public static DeveloperKey FromIdentity(DeveloperIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity.DisplayName, nameof(identity));
        return new DeveloperKey(identity.Uuid ?? throw new ArgumentException("A user UUID is required.", nameof(identity)));
    }

    /// <inheritdoc />
    public bool Equals(DeveloperKey other)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(Uuid?.Value, other.Uuid?.Value);
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
        return Uuid is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Uuid.Value);
    }

    /// <inheritdoc />
    public override string ToString() => Uuid?.Value ?? string.Empty;
}

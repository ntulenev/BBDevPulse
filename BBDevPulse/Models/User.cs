namespace BBDevPulse.Models;

/// <summary>
/// Bitbucket user domain model.
/// </summary>
public sealed class User
{
    /// <summary>
    /// Initializes a new instance of the <see cref="User"/> class.
    /// </summary>
    /// <param name="displayName">Display name.</param>
    /// <param name="uuid">User UUID.</param>
    public User(DisplayName displayName, UserUuid uuid)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(uuid);

        DisplayName = displayName;
        Uuid = uuid;
    }

    /// <summary>
    /// Display name.
    /// </summary>
    public DisplayName DisplayName { get; }

    /// <summary>
    /// User UUID.
    /// </summary>
    public UserUuid Uuid { get; }

    /// <summary>
    /// Builds developer identity for this user.
    /// </summary>
    public DeveloperIdentity ToDeveloperIdentity() => new(Uuid, DisplayName);
}

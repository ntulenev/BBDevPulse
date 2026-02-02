namespace BBDevPulse.Models;

/// <summary>
/// Authenticated Bitbucket user domain model.
/// </summary>
public sealed class AuthUser
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthUser"/> class.
    /// </summary>
    /// <param name="displayName">Display name.</param>
    /// <param name="username">Username.</param>
    /// <param name="uuid">User UUID.</param>
    public AuthUser(DisplayName displayName, Username username, UserUuid uuid)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(uuid);

        DisplayName = displayName;
        Username = username;
        Uuid = uuid;
    }

    /// <summary>
    /// Display name.
    /// </summary>
    public DisplayName DisplayName { get; }

    /// <summary>
    /// Username.
    /// </summary>
    public Username Username { get; }

    /// <summary>
    /// User UUID.
    /// </summary>
    public UserUuid Uuid { get; }
}

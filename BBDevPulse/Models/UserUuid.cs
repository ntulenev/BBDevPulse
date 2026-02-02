namespace BBDevPulse.Models;

/// <summary>
/// User UUID domain model.
/// </summary>
public sealed class UserUuid
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserUuid"/> class.
    /// </summary>
    /// <param name="value">UUID value.</param>
    public UserUuid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("User UUID must be provided.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// UUID value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

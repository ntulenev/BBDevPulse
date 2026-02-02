namespace BBDevPulse.Models;

/// <summary>
/// Username domain model.
/// </summary>
public sealed class Username
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Username"/> class.
    /// </summary>
    /// <param name="value">Username value.</param>
    public Username(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Username must be provided.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Username value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

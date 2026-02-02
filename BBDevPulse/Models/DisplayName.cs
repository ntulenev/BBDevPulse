namespace BBDevPulse.Models;

/// <summary>
/// Display name domain model.
/// </summary>
public sealed class DisplayName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayName"/> class.
    /// </summary>
    /// <param name="value">Display name value.</param>
    public DisplayName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Display name must be provided.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Display name value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

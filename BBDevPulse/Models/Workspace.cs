namespace BBDevPulse.Models;

/// <summary>
/// Bitbucket workspace domain model.
/// </summary>
public sealed class Workspace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Workspace"/> class.
    /// </summary>
    /// <param name="value">Workspace identifier.</param>
    public Workspace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workspace value must be provided.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Workspace identifier.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

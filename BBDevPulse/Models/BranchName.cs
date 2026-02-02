namespace BBDevPulse.Models;

/// <summary>
/// Branch name domain model.
/// </summary>
public sealed class BranchName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BranchName"/> class.
    /// </summary>
    /// <param name="value">Branch name.</param>
    public BranchName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Branch name must be provided.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Branch name value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

namespace BBDevPulse.Models;

/// <summary>
/// Repository name filter domain model.
/// </summary>
public sealed class RepoNameFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepoNameFilter"/> class.
    /// </summary>
    /// <param name="value">Filter value.</param>
    public RepoNameFilter(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>
    /// Filter value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

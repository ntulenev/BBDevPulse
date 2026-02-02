namespace BBDevPulse.Models;

/// <summary>
/// Repository name domain model.
/// </summary>
public sealed class RepoName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepoName"/> class.
    /// </summary>
    /// <param name="value">Repository name.</param>
    public RepoName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Repository name must be provided.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Repository name.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

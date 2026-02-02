namespace BBDevPulse.Models;

/// <summary>
/// Bitbucket repository slug domain model.
/// </summary>
public sealed class RepoSlug
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepoSlug"/> class.
    /// </summary>
    /// <param name="value">Repository slug.</param>
    public RepoSlug(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Repository slug must be provided.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Repository slug value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

namespace BBDevPulse.Models;

/// <summary>
/// Pull request commit information used in report analysis.
/// </summary>
public sealed class PullRequestCommitInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestCommitInfo"/> class.
    /// </summary>
    /// <param name="hash">Commit hash.</param>
    /// <param name="date">Commit timestamp.</param>
    public PullRequestCommitInfo(string hash, DateTimeOffset date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        Hash = hash;
        Date = date;
    }

    /// <summary>
    /// Gets the commit hash.
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// Gets the commit timestamp.
    /// </summary>
    public DateTimeOffset Date { get; }
}

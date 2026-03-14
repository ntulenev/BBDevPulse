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
    /// <param name="message">Commit message.</param>
    public PullRequestCommitInfo(string hash, DateTimeOffset date, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Hash = hash;
        Date = date;
        Message = message;
    }

    /// <summary>
    /// Gets the commit hash.
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// Gets the commit timestamp.
    /// </summary>
    public DateTimeOffset Date { get; }

    /// <summary>
    /// Gets the commit message.
    /// </summary>
    public string Message { get; }
}

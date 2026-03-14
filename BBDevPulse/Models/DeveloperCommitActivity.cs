namespace BBDevPulse.Models;

/// <summary>
/// Detailed follow-up commit activity for a developer.
/// </summary>
public sealed class DeveloperCommitActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperCommitActivity"/> class.
    /// </summary>
    /// <param name="repository">Repository name.</param>
    /// <param name="repositorySlug">Repository slug.</param>
    /// <param name="pullRequestId">Pull request identifier.</param>
    /// <param name="commitHash">Commit hash.</param>
    /// <param name="message">Commit message.</param>
    /// <param name="date">Commit timestamp.</param>
    /// <param name="sizeSummary">Commit diffstat summary.</param>
    public DeveloperCommitActivity(
        string repository,
        string repositorySlug,
        PullRequestId pullRequestId,
        string commitHash,
        string message,
        DateTimeOffset date,
        PullRequestSizeSummary sizeSummary)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(repositorySlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Repository = repository;
        RepositorySlug = repositorySlug;
        PullRequestId = pullRequestId;
        CommitHash = commitHash;
        Message = message;
        Date = date;
        SizeSummary = sizeSummary;
    }

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public string Repository { get; }

    /// <summary>
    /// Gets the repository slug.
    /// </summary>
    public string RepositorySlug { get; }

    /// <summary>
    /// Gets the pull request identifier.
    /// </summary>
    public PullRequestId PullRequestId { get; }

    /// <summary>
    /// Gets the commit timestamp.
    /// </summary>
    public DateTimeOffset Date { get; }

    /// <summary>
    /// Gets the commit hash.
    /// </summary>
    public string CommitHash { get; }

    /// <summary>
    /// Gets the commit message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the commit diffstat summary.
    /// </summary>
    public PullRequestSizeSummary SizeSummary { get; }

    /// <summary>
    /// Determines whether size data is available for the configured mode.
    /// </summary>
    /// <param name="mode">Size mode.</param>
    /// <returns><see langword="true"/> when data is available for the selected mode.</returns>
    public bool HasSizeDataForMode(PullRequestSizeMode mode)
    {
        return mode switch
        {
            PullRequestSizeMode.Lines => SizeSummary.LineChurn > 0,
            PullRequestSizeMode.Files => SizeSummary.FilesChanged > 0,
            _ => SizeSummary.LineChurn > 0
        };
    }

    /// <summary>
    /// Gets the size metric value for the selected mode.
    /// </summary>
    /// <param name="mode">Size mode.</param>
    /// <returns>Metric value for the mode.</returns>
    public int GetSizeMetricValue(PullRequestSizeMode mode)
    {
        return mode switch
        {
            PullRequestSizeMode.Lines => SizeSummary.LineChurn,
            PullRequestSizeMode.Files => SizeSummary.FilesChanged,
            _ => SizeSummary.LineChurn
        };
    }
}

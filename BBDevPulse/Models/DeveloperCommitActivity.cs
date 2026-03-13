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
    /// <param name="date">Commit timestamp.</param>
    public DeveloperCommitActivity(
        string repository,
        string repositorySlug,
        PullRequestId pullRequestId,
        string commitHash,
        DateTimeOffset date)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(repositorySlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitHash);

        Repository = repository;
        RepositorySlug = repositorySlug;
        PullRequestId = pullRequestId;
        CommitHash = commitHash;
        Date = date;
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
}

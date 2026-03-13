namespace BBDevPulse.Models;

/// <summary>
/// Detailed comment activity for a developer.
/// </summary>
public sealed class DeveloperCommentActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperCommentActivity"/> class.
    /// </summary>
    /// <param name="repository">Repository name.</param>
    /// <param name="repositorySlug">Repository slug.</param>
    /// <param name="pullRequestId">Pull request identifier.</param>
    /// <param name="pullRequestAuthor">Pull request author.</param>
    /// <param name="date">Comment timestamp.</param>
    public DeveloperCommentActivity(
        string repository,
        string repositorySlug,
        PullRequestId pullRequestId,
        string pullRequestAuthor,
        DateTimeOffset date)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(repositorySlug);
        ArgumentNullException.ThrowIfNull(pullRequestAuthor);

        Repository = repository;
        RepositorySlug = repositorySlug;
        PullRequestId = pullRequestId;
        PullRequestAuthor = pullRequestAuthor;
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
    /// Gets the pull request author.
    /// </summary>
    public string PullRequestAuthor { get; }

    /// <summary>
    /// Gets the comment timestamp.
    /// </summary>
    public DateTimeOffset Date { get; }
}

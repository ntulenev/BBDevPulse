namespace BBDevPulse.Models;

/// <summary>
/// Describes a pull request entry in the report.
/// </summary>
public sealed class PullRequestReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestReport"/> class.
    /// </summary>
    /// <param name="repository">Repository name.</param>
    /// <param name="author">Pull request author.</param>
    /// <param name="targetBranch">Destination branch name.</param>
    /// <param name="createdOn">Pull request creation timestamp.</param>
    /// <param name="lastActivity">Last activity timestamp.</param>
    /// <param name="mergedOn">Merge timestamp, when available.</param>
    /// <param name="rejectedOn">Rejection timestamp, when available.</param>
    /// <param name="state">Pull request state.</param>
    /// <param name="id">Pull request identifier.</param>
    /// <param name="comments">Total comment count.</param>
    /// <param name="firstReactionOn">First non-author reaction timestamp, when available.</param>
    public PullRequestReport(
        string repository,
        string author,
        string targetBranch,
        DateTimeOffset createdOn,
        DateTimeOffset lastActivity,
        DateTimeOffset? mergedOn,
        DateTimeOffset? rejectedOn,
        PullRequestState state,
        PullRequestId id,
        int comments,
        DateTimeOffset? firstReactionOn)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(targetBranch);

        Repository = repository;
        Author = author;
        TargetBranch = targetBranch;
        CreatedOn = createdOn;
        LastActivity = lastActivity;
        MergedOn = mergedOn;
        RejectedOn = rejectedOn;
        State = state;
        Id = id;
        Comments = comments;
        FirstReactionOn = firstReactionOn;
    }

    /// <summary>
    /// Repository name.
    /// </summary>
    public string Repository { get; }

    /// <summary>
    /// Pull request author.
    /// </summary>
    public string Author { get; }

    /// <summary>
    /// Destination branch name.
    /// </summary>
    public string TargetBranch { get; }

    /// <summary>
    /// Pull request creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedOn { get; }

    /// <summary>
    /// Last activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivity { get; }

    /// <summary>
    /// Merge timestamp, when available.
    /// </summary>
    public DateTimeOffset? MergedOn { get; }

    /// <summary>
    /// Rejection timestamp, when available.
    /// </summary>
    public DateTimeOffset? RejectedOn { get; }

    /// <summary>
    /// Pull request state.
    /// </summary>
    public PullRequestState State { get; }

    /// <summary>
    /// Pull request identifier.
    /// </summary>
    public PullRequestId Id { get; }

    /// <summary>
    /// Total comment count.
    /// </summary>
    public int Comments { get; }

    /// <summary>
    /// First non-author reaction timestamp, when available.
    /// </summary>
    public DateTimeOffset? FirstReactionOn { get; }
}

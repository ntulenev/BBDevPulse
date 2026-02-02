namespace BBDevPulse.Models;

/// <summary>
/// Pull request domain model.
/// </summary>
public sealed class PullRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequest"/> class.
    /// </summary>
    /// <param name="id">Pull request identifier.</param>
    /// <param name="state">Pull request state.</param>
    /// <param name="closedOn">Closed timestamp.</param>
    /// <param name="createdOn">Creation timestamp.</param>
    /// <param name="updatedOn">Last updated timestamp.</param>
    /// <param name="mergedOn">Merge timestamp.</param>
    /// <param name="author">Pull request author.</param>
    /// <param name="destination">Pull request destination.</param>
    public PullRequest(
        PullRequestId id,
        PullRequestState state,
        DateTimeOffset? closedOn,
        DateTimeOffset createdOn,
        DateTimeOffset? updatedOn,
        DateTimeOffset? mergedOn,
        User? author,
        PullRequestDestination? destination)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
        State = state;
        ClosedOn = closedOn;
        CreatedOn = createdOn;
        UpdatedOn = updatedOn;
        MergedOn = mergedOn;
        Author = author;
        Destination = destination;
    }

    /// <summary>
    /// Pull request identifier.
    /// </summary>
    public PullRequestId Id { get; }

    /// <summary>
    /// Pull request state.
    /// </summary>
    public PullRequestState State { get; }

    /// <summary>
    /// Closed timestamp.
    /// </summary>
    public DateTimeOffset? ClosedOn { get; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedOn { get; }

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedOn { get; }

    /// <summary>
    /// Merge timestamp.
    /// </summary>
    public DateTimeOffset? MergedOn { get; }

    /// <summary>
    /// Pull request author.
    /// </summary>
    public User? Author { get; }

    /// <summary>
    /// Pull request destination.
    /// </summary>
    public PullRequestDestination? Destination { get; }
}

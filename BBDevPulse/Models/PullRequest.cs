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

    /// <summary>
    /// Determines whether a pull request should stop processing based on time filter mode.
    /// </summary>
    /// <param name="filterDate">Filter cutoff date.</param>
    /// <param name="filterMode">Filter mode.</param>
    /// <returns>True when processing should stop.</returns>
    public bool ShouldStopByTimeFilter(DateTimeOffset filterDate, PrTimeFilterMode filterMode)
    {
        return filterMode switch
        {
            PrTimeFilterMode.CreatedOnOnly => CreatedOn < filterDate,
            PrTimeFilterMode.LastKnownUpdateAndCreated =>
                (UpdatedOn ?? CreatedOn) < filterDate &&
                CreatedOn < filterDate,
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Determines whether the pull request matches the branch filter list.
    /// </summary>
    /// <param name="branchList">Branch names to filter by.</param>
    /// <returns>True when the pull request should be included.</returns>
    public bool MatchesBranchFilter(IReadOnlyList<BranchName> branchList)
    {
        ArgumentNullException.ThrowIfNull(branchList);

        if (branchList.Count == 0)
        {
            return true;
        }

        var targetBranch = Destination?.Branch?.Name;
        if (string.IsNullOrWhiteSpace(targetBranch))
        {
            return false;
        }

        return branchList.Any(branch =>
            targetBranch.Equals(branch.Value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves the rejection timestamp when the pull request is declined or superseded.
    /// </summary>
    /// <returns>Rejection timestamp if applicable; otherwise null.</returns>
    public DateTimeOffset? ResolveRejectedOn()
    {
        return State is not PullRequestState.Declined and
            not PullRequestState.Superseded
            ? null
            : ClosedOn ?? UpdatedOn;
    }
}

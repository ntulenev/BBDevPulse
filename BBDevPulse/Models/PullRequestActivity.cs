namespace BBDevPulse.Models;

/// <summary>
/// Pull request activity domain model.
/// </summary>
public sealed class PullRequestActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestActivity"/> class.
    /// </summary>
    /// <param name="activityDate">Activity timestamp.</param>
    /// <param name="mergeDate">Merge timestamp, if the activity represents a merge.</param>
    /// <param name="actor">Activity user, if available.</param>
    /// <param name="comment">Comment details, if the activity is a comment.</param>
    /// <param name="approval">Approval details, if the activity is an approval.</param>
    public PullRequestActivity(
        DateTimeOffset? activityDate,
        DateTimeOffset? mergeDate,
        DeveloperIdentity? actor,
        ActivityComment? comment,
        ActivityApproval? approval)
    {
        ActivityDate = activityDate;
        MergeDate = mergeDate;
        Actor = actor;
        Comment = comment;
        Approval = approval;
    }

    /// <summary>
    /// Activity timestamp.
    /// </summary>
    public DateTimeOffset? ActivityDate { get; }

    /// <summary>
    /// Merge timestamp, if the activity represents a merge.
    /// </summary>
    public DateTimeOffset? MergeDate { get; }

    /// <summary>
    /// Activity user, if available.
    /// </summary>
    public DeveloperIdentity? Actor { get; }

    /// <summary>
    /// Comment details, if the activity is a comment.
    /// </summary>
    public ActivityComment? Comment { get; }

    /// <summary>
    /// Approval details, if the activity is an approval.
    /// </summary>
    public ActivityApproval? Approval { get; }

    /// <summary>
    /// Returns true when the activity date is before the provided date.
    /// </summary>
    /// <param name="date">Filter date.</param>
    public bool IsBefore(DateTimeOffset date) =>
        ActivityDate.HasValue && ActivityDate.Value < date;

    /// <summary>
    /// Attempts to update the last activity timestamp.
    /// </summary>
    /// <param name="lastActivity">Current last activity timestamp.</param>
    /// <returns>True when the timestamp was updated.</returns>
    public bool TryUpdateLastActivity(ref DateTimeOffset lastActivity)
    {
        if (!ActivityDate.HasValue)
        {
            return false;
        }

        var activityDate = ActivityDate.Value;
        if (activityDate <= lastActivity)
        {
            return false;
        }

        lastActivity = activityDate;
        return true;
    }

    /// <summary>
    /// Attempts to update the resolved merge timestamp.
    /// </summary>
    /// <param name="mergedOn">Current resolved merge timestamp.</param>
    /// <returns>True when the timestamp was updated.</returns>
    public bool TryUpdateMergedOn(ref DateTimeOffset? mergedOn)
    {
        if (!MergeDate.HasValue)
        {
            return false;
        }

        var mergeDate = MergeDate.Value;
        if (mergedOn.HasValue && mergeDate >= mergedOn.Value)
        {
            return false;
        }

        mergedOn = mergeDate;
        return true;
    }

    /// <summary>
    /// Attempts to read the activity actor.
    /// </summary>
    /// <param name="actor">Resolved actor.</param>
    /// <returns>True when the actor exists.</returns>
    public bool TryGetActor(out DeveloperIdentity actor)
    {
        if (Actor is null)
        {
            actor = default;
            return false;
        }

        actor = Actor.Value;
        return true;
    }
}

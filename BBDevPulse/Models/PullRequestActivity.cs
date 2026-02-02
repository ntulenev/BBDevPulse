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
}

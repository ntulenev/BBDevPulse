namespace BBDevPulse.Models;

/// <summary>
/// Pull request approval activity details.
/// </summary>
public sealed class ActivityApproval
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityApproval"/> class.
    /// </summary>
    /// <param name="user">Approval author.</param>
    /// <param name="date">Approval timestamp.</param>
    public ActivityApproval(DeveloperIdentity user, DateTimeOffset date)
    {
        User = user;
        Date = date;
    }

    /// <summary>
    /// Approval author.
    /// </summary>
    public DeveloperIdentity User { get; }

    /// <summary>
    /// Approval timestamp.
    /// </summary>
    public DateTimeOffset Date { get; }

    /// <summary>
    /// Returns true when the approval happened on or after the given date.
    /// </summary>
    /// <param name="date">Filter date.</param>
    public bool IsOnOrAfter(DateTimeOffset date) => Date >= date;

    /// <summary>
    /// Returns true when the approval author differs from the provided identity.
    /// </summary>
    /// <param name="authorIdentity">Author identity.</param>
    public bool IsByDifferentDeveloper(DeveloperIdentity? authorIdentity) =>
        !authorIdentity.HasValue || !authorIdentity.Value.IsSameIdentity(User);

    /// <summary>
    /// Attempts to update the first reaction timestamp based on this approval.
    /// </summary>
    /// <param name="firstReactionOn">Current first reaction timestamp.</param>
    /// <returns>True when the timestamp was updated.</returns>
    public bool TryUpdateFirstReaction(ref DateTimeOffset? firstReactionOn)
    {
        if (!firstReactionOn.HasValue || Date < firstReactionOn.Value)
        {
            firstReactionOn = Date;
            return true;
        }

        return false;
    }
}

namespace BBDevPulse.Models;

/// <summary>
/// Pull request comment activity details.
/// </summary>
public sealed class ActivityComment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityComment"/> class.
    /// </summary>
    /// <param name="user">Comment author.</param>
    /// <param name="date">Comment timestamp.</param>
    public ActivityComment(DeveloperIdentity user, DateTimeOffset date)
    {
        User = user;
        Date = date;
    }

    /// <summary>
    /// Comment author.
    /// </summary>
    public DeveloperIdentity User { get; }

    /// <summary>
    /// Comment timestamp.
    /// </summary>
    public DateTimeOffset Date { get; }

    /// <summary>
    /// Returns true when the comment happened on or after the given date.
    /// </summary>
    /// <param name="date">Filter date.</param>
    public bool IsOnOrAfter(DateTimeOffset date) => Date >= date;

    /// <summary>
    /// Returns true when the comment author differs from the provided identity.
    /// </summary>
    /// <param name="authorIdentity">Author identity.</param>
    public bool IsByDifferentDeveloper(DeveloperIdentity? authorIdentity) =>
        !authorIdentity.HasValue || !authorIdentity.Value.IsSameIdentity(User);

    /// <summary>
    /// Attempts to update the first reaction timestamp based on this comment.
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

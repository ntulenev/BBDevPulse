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
}

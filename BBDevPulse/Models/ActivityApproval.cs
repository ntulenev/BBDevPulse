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
}

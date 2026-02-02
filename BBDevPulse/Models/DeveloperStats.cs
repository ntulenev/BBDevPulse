namespace BBDevPulse.Models;

/// <summary>
/// Aggregated statistics for a developer.
/// </summary>
public sealed class DeveloperStats
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperStats"/> class.
    /// </summary>
    /// <param name="displayName">Developer display name.</param>
    public DeveloperStats(DisplayName displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        DisplayName = displayName;
    }

    /// <summary>
    /// Developer display name.
    /// </summary>
    public DisplayName DisplayName { get; set; }

    /// <summary>
    /// Pull requests opened since the filter date.
    /// </summary>
    public int PrsOpenedSince { get; set; }

    /// <summary>
    /// Pull requests merged after the filter date.
    /// </summary>
    public int PrsMergedAfter { get; set; }

    /// <summary>
    /// Comments added after the filter date.
    /// </summary>
    public int CommentsAfter { get; set; }

    /// <summary>
    /// Approvals added after the filter date.
    /// </summary>
    public int ApprovalsAfter { get; set; }
}

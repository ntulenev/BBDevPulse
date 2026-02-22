namespace BBDevPulse.Models;

/// <summary>
/// Aggregated statistics for a developer.
/// </summary>
public sealed class DeveloperStats
{
    /// <summary>
    /// Default value for missing developer enrichment fields.
    /// </summary>
    public const string NotAvailable = "N/A";

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
    /// Developer grade from CSV enrichment.
    /// </summary>
    public string Grade { get; set; } = NotAvailable;

    /// <summary>
    /// Developer department from CSV enrichment.
    /// </summary>
    public string Department { get; set; } = NotAvailable;

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

    /// <summary>
    /// Corrections count across pull requests opened by the developer.
    /// </summary>
    public int Corrections { get; set; }
}

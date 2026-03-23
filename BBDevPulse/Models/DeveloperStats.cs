namespace BBDevPulse.Models;

/// <summary>
/// Aggregated statistics for a developer.
/// </summary>
public sealed class DeveloperStats
{
    /// <summary>
    /// Default value for missing developer enrichment fields.
    /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
    public const string NOT_AVAILABLE = "N/A";
#pragma warning restore CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperStats"/> class.
    /// </summary>
    /// <param name="displayName">Developer display name.</param>
    /// <param name="bitbucketUuid">Developer Bitbucket UUID.</param>
    public DeveloperStats(DisplayName displayName, UserUuid? bitbucketUuid = null)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        DisplayName = displayName;
        BitbucketUuid = bitbucketUuid;
    }

    /// <summary>
    /// Developer display name.
    /// </summary>
    public DisplayName DisplayName { get; set; }

    /// <summary>
    /// Developer grade from CSV enrichment.
    /// </summary>
    public string Grade { get; set; } = NOT_AVAILABLE;

    /// <summary>
    /// Developer Bitbucket UUID.
    /// </summary>
    public UserUuid? BitbucketUuid { get; set; }

    /// <summary>
    /// Developer department from CSV enrichment.
    /// </summary>
    public string Department { get; set; } = NOT_AVAILABLE;

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
    /// Comments added after the filter date on pull requests authored by other developers.
    /// </summary>
    public int PeerCommentsAfter { get; set; }

    /// <summary>
    /// Approvals added after the filter date.
    /// </summary>
    public int ApprovalsAfter { get; set; }

    /// <summary>
    /// Corrections count across pull requests opened by the developer.
    /// </summary>
    public int Corrections { get; set; }

    /// <summary>
    /// Detailed list of pull requests authored by the developer.
    /// </summary>
    public List<PullRequestReport> AuthoredPullRequests { get; } = [];

    /// <summary>
    /// Detailed list of comments authored by the developer.
    /// </summary>
    public List<DeveloperCommentActivity> CommentActivities { get; } = [];

    /// <summary>
    /// Detailed list of approvals authored by the developer.
    /// </summary>
    public List<DeveloperApprovalActivity> ApprovalActivities { get; } = [];

    /// <summary>
    /// Detailed list of follow-up commits made on the developer's pull requests after creation.
    /// </summary>
    public List<DeveloperCommitActivity> CommitActivities { get; } = [];
}

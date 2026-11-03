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
    public DisplayName DisplayName { get; }

    /// <summary>
    /// Developer grade from CSV enrichment.
    /// </summary>
    public string Grade { get; set { EnsureMutable(); field = value; } } = NOT_AVAILABLE;

    /// <summary>
    /// Developer Bitbucket UUID.
    /// </summary>
    public UserUuid? BitbucketUuid { get; private set; }

    internal void AttachUuid(UserUuid uuid)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(uuid);
        if (BitbucketUuid is not null)
        {
            throw new InvalidOperationException("The developer already has a UUID.");
        }

        BitbucketUuid = uuid;
    }

    /// <summary>
    /// Developer department from CSV enrichment.
    /// </summary>
    public string Department { get; set { EnsureMutable(); field = value; } } = NOT_AVAILABLE;

    /// <summary>
    /// Pull requests opened since the filter date.
    /// </summary>
    public int PrsOpenedSince {
        get;
        set
        {
            EnsureMutable();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// Pull requests merged after the filter date.
    /// </summary>
    public int PrsMergedAfter {
        get;
        set
        {
            EnsureMutable();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// Comments added after the filter date.
    /// </summary>
    public int CommentsAfter {
        get;
        set
        {
            EnsureMutable();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// Comments added after the filter date on pull requests authored by other developers.
    /// </summary>
    public int PeerCommentsAfter {
        get;
        set
        {
            EnsureMutable();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// Approvals added after the filter date.
    /// </summary>
    public int ApprovalsAfter {
        get;
        set
        {
            EnsureMutable();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// Corrections count across pull requests opened by the developer.
    /// </summary>
    public int Corrections {
        get;
        set
        {
            EnsureMutable();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// Whether correction counts were available for every authored pull request.
    /// </summary>
    public bool HasCompleteCorrectionsData { get; set { EnsureMutable(); field = value; } } = true;

    /// <summary>
    /// Detailed list of pull requests authored by the developer.
    /// </summary>
    public IList<PullRequestReport> AuthoredPullRequests { get; private set; } = new List<PullRequestReport>();

    /// <summary>
    /// Detailed list of comments authored by the developer.
    /// </summary>
    public IList<DeveloperCommentActivity> CommentActivities { get; private set; } = new List<DeveloperCommentActivity>();

    /// <summary>
    /// Detailed list of approvals authored by the developer.
    /// </summary>
    public IList<DeveloperApprovalActivity> ApprovalActivities { get; private set; } = new List<DeveloperApprovalActivity>();

    /// <summary>
    /// Detailed list of follow-up commits made on the developer's pull requests after creation.
    /// </summary>
    public IList<DeveloperCommitActivity> CommitActivities { get; private set; } = new List<DeveloperCommitActivity>();

    internal void Freeze()
    {
        AuthoredPullRequests = Array.AsReadOnly(AuthoredPullRequests.ToArray());
        CommentActivities = Array.AsReadOnly(CommentActivities.ToArray());
        ApprovalActivities = Array.AsReadOnly(ApprovalActivities.ToArray());
        CommitActivities = Array.AsReadOnly(CommitActivities.ToArray());
        _frozen = true;
    }

    private void EnsureMutable()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Published statistics cannot be modified.");
        }
    }

    private bool _frozen;
}

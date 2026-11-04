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
        AuthoredPullRequests = new System.Collections.ObjectModel.ReadOnlyCollection<PullRequestReport>(_authoredPullRequests);
        CommentActivities = new System.Collections.ObjectModel.ReadOnlyCollection<DeveloperCommentActivity>(_commentActivities);
        ApprovalActivities = new System.Collections.ObjectModel.ReadOnlyCollection<DeveloperApprovalActivity>(_approvalActivities);
        CommitActivities = new System.Collections.ObjectModel.ReadOnlyCollection<DeveloperCommitActivity>(_commitActivities);
    }

    /// <summary>
    /// Developer display name.
    /// </summary>
    public DisplayName DisplayName { get; }

    /// <summary>
    /// Developer grade from CSV enrichment.
    /// </summary>
    public string Grade { get; internal set { EnsureMutable(); ArgumentNullException.ThrowIfNull(value); field = value; } } = NOT_AVAILABLE;

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
    public string Department { get; internal set { EnsureMutable(); ArgumentNullException.ThrowIfNull(value); field = value; } } = NOT_AVAILABLE;

    /// <summary>
    /// Pull requests opened since the filter date.
    /// </summary>
    public int PrsOpenedSince {
        get;
        internal set
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
        internal set
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
        internal set
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
        internal set
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
        internal set
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
        internal set
        {
            EnsureMutable();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// Whether correction counts were available for every authored pull request.
    /// </summary>
    public bool HasCompleteCorrectionsData { get; internal set { EnsureMutable(); field = value; } } = true;

    /// <summary>Whether all correction data used by this aggregate was available.</summary>
    public DataAvailability CorrectionsAvailability => HasCompleteCorrectionsData
        ? DataAvailability.Available : DataAvailability.Incomplete;

    /// <summary>
    /// Detailed list of pull requests authored by the developer.
    /// </summary>
    public IReadOnlyList<PullRequestReport> AuthoredPullRequests { get; private set; } = null!;

    private IList<PullRequestReport> _authoredPullRequests = new List<PullRequestReport>();

    internal IList<PullRequestReport> MutableAuthoredPullRequests {
        get
        {
            EnsureMutable();
            return _authoredPullRequests;
        }
    }

    /// <summary>
    /// Detailed list of comments authored by the developer.
    /// </summary>
    public IReadOnlyList<DeveloperCommentActivity> CommentActivities { get; private set; } = null!;

    private IList<DeveloperCommentActivity> _commentActivities = new List<DeveloperCommentActivity>();

    internal IList<DeveloperCommentActivity> MutableCommentActivities {
        get
        {
            EnsureMutable();
            return _commentActivities;
        }
    }

    /// <summary>
    /// Detailed list of approvals authored by the developer.
    /// </summary>
    public IReadOnlyList<DeveloperApprovalActivity> ApprovalActivities { get; private set; } = null!;

    private IList<DeveloperApprovalActivity> _approvalActivities = new List<DeveloperApprovalActivity>();

    internal IList<DeveloperApprovalActivity> MutableApprovalActivities {
        get
        {
            EnsureMutable();
            return _approvalActivities;
        }
    }

    /// <summary>
    /// Detailed list of follow-up commits made on the developer's pull requests after creation.
    /// </summary>
    public IReadOnlyList<DeveloperCommitActivity> CommitActivities { get; private set; } = null!;

    private IList<DeveloperCommitActivity> _commitActivities = new List<DeveloperCommitActivity>();

    internal IList<DeveloperCommitActivity> MutableCommitActivities {
        get
        {
            EnsureMutable();
            return _commitActivities;
        }
    }

    internal DeveloperStats Copy()
    {
        var copy = new DeveloperStats(DisplayName, BitbucketUuid)
        {
            Grade = Grade,
            Department = Department,
            PrsOpenedSince = PrsOpenedSince,
            PrsMergedAfter = PrsMergedAfter,
            CommentsAfter = CommentsAfter,
            PeerCommentsAfter = PeerCommentsAfter,
            ApprovalsAfter = ApprovalsAfter,
            Corrections = Corrections,
            HasCompleteCorrectionsData = HasCompleteCorrectionsData
        };
        foreach (var report in AuthoredPullRequests.OrderBy(static item => item.CreatedOn)
            .ThenBy(static item => item.RepositorySlug, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.Id.Value))
            copy.MutableAuthoredPullRequests.Add(report);
        foreach (var comment in CommentActivities.OrderBy(static item => item.Date)
            .ThenBy(static item => item.RepositorySlug, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.PullRequestId.Value))
            copy.MutableCommentActivities.Add(comment);
        foreach (var approval in ApprovalActivities.OrderBy(static item => item.Date)
            .ThenBy(static item => item.RepositorySlug, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.PullRequestId.Value))
            copy.MutableApprovalActivities.Add(approval);
        foreach (var commit in CommitActivities.OrderBy(static item => item.Date)
            .ThenBy(static item => item.RepositorySlug, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.PullRequestId.Value)
            .ThenBy(static item => item.CommitHash, StringComparer.Ordinal))
            copy.MutableCommitActivities.Add(commit);
        return copy;
    }

    internal void Freeze()
    {
        _authoredPullRequests = AuthoredPullRequests.ToList();
        AuthoredPullRequests = new System.Collections.ObjectModel.ReadOnlyCollection<PullRequestReport>(_authoredPullRequests);
        _commentActivities = CommentActivities.ToList();
        CommentActivities = new System.Collections.ObjectModel.ReadOnlyCollection<DeveloperCommentActivity>(_commentActivities);
        _approvalActivities = ApprovalActivities.ToList();
        ApprovalActivities = new System.Collections.ObjectModel.ReadOnlyCollection<DeveloperApprovalActivity>(_approvalActivities);
        _commitActivities = CommitActivities.ToList();
        CommitActivities = new System.Collections.ObjectModel.ReadOnlyCollection<DeveloperCommitActivity>(_commitActivities);
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

namespace BBDevPulse.Models;

/// <summary>
/// Describes a pull request entry in the report.
/// </summary>
public sealed class PullRequestReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestReport"/> class.
    /// </summary>
    /// <param name="repository">Repository name.</param>
    /// <param name="repositorySlug">Repository slug.</param>
    /// <param name="author">Pull request author.</param>
    /// <param name="targetBranch">Destination branch name.</param>
    /// <param name="createdOn">Pull request creation timestamp.</param>
    /// <param name="lastActivity">Last activity timestamp.</param>
    /// <param name="mergedOn">Merge timestamp, when available.</param>
    /// <param name="rejectedOn">Rejection timestamp, when available.</param>
    /// <param name="state">Pull request state.</param>
    /// <param name="id">Pull request identifier.</param>
    /// <param name="comments">Total comment count.</param>
    /// <param name="corrections">Total corrections count.</param>
    /// <param name="firstReactionOn">First non-author reaction timestamp, when available.</param>
    /// <param name="filesChanged">Total files changed in pull request diffstat.</param>
    /// <param name="linesAdded">Total added lines in pull request diffstat.</param>
    /// <param name="linesRemoved">Total removed lines in pull request diffstat.</param>
    /// <param name="isActivityOnlyMatch">Whether this pull request is shown because the selected team was active on it.</param>
    public PullRequestReport(
        string repository,
        string repositorySlug,
        string author,
        string targetBranch,
        DateTimeOffset createdOn,
        DateTimeOffset lastActivity,
        DateTimeOffset? mergedOn,
        DateTimeOffset? rejectedOn,
        PullRequestState state,
        PullRequestId id,
        int comments,
        int corrections = 0,
        DateTimeOffset? firstReactionOn = null,
        int filesChanged = 0,
        int linesAdded = 0,
        int linesRemoved = 0,
        bool isActivityOnlyMatch = false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(repositorySlug);
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(targetBranch);

        Repository = repository;
        RepositorySlug = repositorySlug;
        Author = author;
        TargetBranch = targetBranch;
        CreatedOn = createdOn;
        LastActivity = lastActivity;
        MergedOn = mergedOn;
        RejectedOn = rejectedOn;
        State = state;
        Id = id;
        Comments = comments;
        Corrections = corrections;
        FirstReactionOn = firstReactionOn;
        FilesChanged = filesChanged;
        LinesAdded = linesAdded;
        LinesRemoved = linesRemoved;
        IsActivityOnlyMatch = isActivityOnlyMatch;
    }

    /// <summary>
    /// Repository name.
    /// </summary>
    public string Repository { get; }

    /// <summary>
    /// Repository slug.
    /// </summary>
    public string RepositorySlug { get; }

    /// <summary>
    /// Pull request author.
    /// </summary>
    public string Author { get; }

    /// <summary>
    /// Destination branch name.
    /// </summary>
    public string TargetBranch { get; }

    /// <summary>
    /// Pull request creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedOn { get; }

    /// <summary>
    /// Last activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivity { get; }

    /// <summary>
    /// Merge timestamp, when available.
    /// </summary>
    public DateTimeOffset? MergedOn { get; }

    /// <summary>
    /// Rejection timestamp, when available.
    /// </summary>
    public DateTimeOffset? RejectedOn { get; }

    /// <summary>
    /// Pull request state.
    /// </summary>
    public PullRequestState State { get; }

    /// <summary>
    /// Pull request identifier.
    /// </summary>
    public PullRequestId Id { get; }

    /// <summary>
    /// Total comment count.
    /// </summary>
    public int Comments { get; }

    /// <summary>
    /// Total corrections count.
    /// </summary>
    public int Corrections { get; }

    /// <summary>
    /// First non-author reaction timestamp, when available.
    /// </summary>
    public DateTimeOffset? FirstReactionOn { get; }

    /// <summary>
    /// Total changed files in pull request diffstat.
    /// </summary>
    public int FilesChanged { get; }

    /// <summary>
    /// Total added lines in pull request diffstat.
    /// </summary>
    public int LinesAdded { get; }

    /// <summary>
    /// Total removed lines in pull request diffstat.
    /// </summary>
    public int LinesRemoved { get; }

    /// <summary>
    /// Total changed lines (added + removed).
    /// </summary>
    public int LineChurn => LinesAdded + LinesRemoved;

    /// <summary>
    /// Gets a value indicating whether the pull request is shown only because the selected team had activity on it.
    /// </summary>
    public bool IsActivityOnlyMatch { get; }

    /// <summary>
    /// Gets a value indicating whether the pull request should be included in PR-based metrics.
    /// </summary>
    public bool IncludeInMetrics => !IsActivityOnlyMatch;

    /// <summary>
    /// Indicates whether size data is available for this pull request.
    /// </summary>
    public bool HasSizeData => FilesChanged > 0 || LinesAdded > 0 || LinesRemoved > 0;

    /// <summary>
    /// Pull request size tier based on churn.
    /// </summary>
    public string SizeTier => PullRequestSizeClassifier.Classify(LineChurn);

    /// <summary>
    /// Indicates whether size data is available for the selected size mode.
    /// </summary>
    /// <param name="mode">Pull request size mode.</param>
    /// <returns><see langword="true"/> when size data can be rendered.</returns>
    public bool HasSizeDataForMode(PullRequestSizeMode mode)
    {
        return mode switch
        {
            PullRequestSizeMode.Lines => HasSizeData,
            PullRequestSizeMode.Files => FilesChanged > 0,
            _ => HasSizeData
        };
    }

    /// <summary>
    /// Gets size metric value for the selected size mode.
    /// </summary>
    /// <param name="mode">Pull request size mode.</param>
    /// <returns>Size metric value.</returns>
    public int GetSizeMetricValue(PullRequestSizeMode mode)
    {
        return mode switch
        {
            PullRequestSizeMode.Lines => LineChurn,
            PullRequestSizeMode.Files => FilesChanged,
            _ => LineChurn
        };
    }

    /// <summary>
    /// Gets pull request size tier for the selected size mode.
    /// </summary>
    /// <param name="mode">Pull request size mode.</param>
    /// <returns>T-shirt size label.</returns>
    public string GetSizeTier(PullRequestSizeMode mode) =>
        PullRequestSizeClassifier.Classify(GetSizeMetricValue(mode), mode);
}

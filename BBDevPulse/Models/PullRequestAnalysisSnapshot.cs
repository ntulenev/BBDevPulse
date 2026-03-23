namespace BBDevPulse.Models;

/// <summary>
/// Cached pull request data sufficient to repeat report analysis without Bitbucket requests.
/// </summary>
public sealed class PullRequestAnalysisSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestAnalysisSnapshot"/> class.
    /// </summary>
    /// <param name="activities">Pull request activities already read from Bitbucket.</param>
    /// <param name="correctionCommits">Correction commits already read from Bitbucket.</param>
    /// <param name="sizeSummary">Pull request size summary.</param>
    /// <param name="commitActivities">Detailed correction commit activities.</param>
    /// <param name="hasEnrichment">Whether correction commits and size data were loaded for this snapshot.</param>
    public PullRequestAnalysisSnapshot(
        IReadOnlyList<PullRequestActivity> activities,
        IReadOnlyList<PullRequestCommitInfo> correctionCommits,
        PullRequestSizeSummary sizeSummary,
        IReadOnlyList<DeveloperCommitActivity> commitActivities,
        bool hasEnrichment = false)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(correctionCommits);
        ArgumentNullException.ThrowIfNull(commitActivities);

        Activities = activities;
        CorrectionCommits = correctionCommits;
        SizeSummary = sizeSummary;
        CommitActivities = commitActivities;
        HasEnrichment = hasEnrichment;
    }

    /// <summary>
    /// Pull request activities already read from Bitbucket.
    /// </summary>
    public IReadOnlyList<PullRequestActivity> Activities { get; }

    /// <summary>
    /// Correction commits already read from Bitbucket.
    /// </summary>
    public IReadOnlyList<PullRequestCommitInfo> CorrectionCommits { get; }

    /// <summary>
    /// Pull request size summary.
    /// </summary>
    public PullRequestSizeSummary SizeSummary { get; }

    /// <summary>
    /// Detailed correction commit activities.
    /// </summary>
    public IReadOnlyList<DeveloperCommitActivity> CommitActivities { get; }

    /// <summary>
    /// Whether correction commits and size data were loaded for this snapshot.
    /// </summary>
    public bool HasEnrichment { get; }
}

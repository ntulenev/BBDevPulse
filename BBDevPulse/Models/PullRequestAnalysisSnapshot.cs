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
    /// <param name="hasCommits">Commit availability, defaulting to hasEnrichment.</param>
    /// <param name="hasSize">Size availability, defaulting to hasEnrichment.</param>
    /// <param name="commitsAvailability">Explicit state of the commit request, if known.</param>
    /// <param name="sizeAvailability">Explicit state of the diffstat request, if known.</param>
    public PullRequestAnalysisSnapshot(
        IReadOnlyList<PullRequestActivity> activities,
        IReadOnlyList<PullRequestCommitInfo> correctionCommits,
        PullRequestSizeSummary sizeSummary,
        IReadOnlyList<DeveloperCommitActivity> commitActivities,
        bool hasEnrichment = false,
        bool? hasCommits = null,
        bool? hasSize = null,
        DataAvailability? commitsAvailability = null,
        DataAvailability? sizeAvailability = null)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(correctionCommits);
        ArgumentNullException.ThrowIfNull(commitActivities);
        if (activities.Any(static item => item is null))
        {
            throw new ArgumentException("Activities cannot contain null entries.", nameof(activities));
        }
        if (correctionCommits.Any(static item => item is null))
        {
            throw new ArgumentException("Commits cannot contain null entries.", nameof(correctionCommits));
        }
        if (commitActivities.Any(static item => item is null))
        {
            throw new ArgumentException("Commit activities cannot contain null entries.", nameof(commitActivities));
        }

        Activities = Array.AsReadOnly(activities.ToArray());
        CorrectionCommits = Array.AsReadOnly(correctionCommits.ToArray());
        SizeSummary = sizeSummary;
        CommitActivities = Array.AsReadOnly(commitActivities.ToArray());
        HasCommits = hasCommits ?? hasEnrichment;
        HasSize = hasSize ?? hasEnrichment;
        CommitsAvailability = ResolveAvailability(commitsAvailability, HasCommits, nameof(commitsAvailability));
        var resolvedSize = sizeAvailability ?? (HasSize && !sizeSummary.HasLineData ? DataAvailability.Incomplete : null);
        SizeAvailability = ResolveAvailability(resolvedSize, HasSize, nameof(sizeAvailability), allowIncomplete: true);
        if (HasSize && (SizeAvailability == DataAvailability.Incomplete) == sizeSummary.HasLineData)
        {
            throw new ArgumentException("Size availability must agree with line completeness.", nameof(sizeAvailability));
        }
        if (hasEnrichment && (!HasCommits || !HasSize || !sizeSummary.HasLineData ||
            commitActivities.Any(static item => !item.HasSizeData || !item.SizeSummary.HasLineData)))
        {
            throw new ArgumentException("Complete enrichment requires commits, size and all detailed commit sizes.", nameof(hasEnrichment));
        }
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

    /// <summary>Availability of the commit list.</summary>
    public DataAvailability CommitsAvailability { get; }

    /// <summary>Availability of the pull request diffstat.</summary>
    public DataAvailability SizeAvailability { get; }

    private static DataAvailability ResolveAvailability(DataAvailability? availability, bool available, string parameter,
        bool allowIncomplete = false)
    {
        var resolved = availability ?? (available ? DataAvailability.Available : DataAvailability.Unknown);
        var hasData = resolved == DataAvailability.Available || (allowIncomplete && resolved == DataAvailability.Incomplete);
        if (!Enum.IsDefined(resolved) || hasData != available)
        {
            throw new ArgumentException("Availability must agree with the data flag.", parameter);
        }
        return resolved;
    }

    /// <summary>Whether the snapshot includes commit data.</summary>
    public bool HasCommits { get; }

    /// <summary>Whether the snapshot includes diffstat data.</summary>
    public bool HasSize { get; }
}

using BBDevPulse.Abstractions;
using BBDevPulse.Caching.Internal.Models;
using BBDevPulse.Models;

namespace BBDevPulse.Caching.Mappers;

/// <summary>
/// Maps pull request analysis snapshots to and from cache models.
/// </summary>
internal sealed class PullRequestAnalysisCacheMapper : IPullRequestAnalysisCacheMapper
{
    /// <inheritdoc />
    public CachedPullRequestAnalysisSnapshot Map(PullRequestAnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new CachedPullRequestAnalysisSnapshot
        {
            Activities = [.. snapshot.Activities.Select(MapActivity)],
            CorrectionCommits = [.. snapshot.CorrectionCommits.Select(MapCommit)],
            SizeSummary = MapSizeSummary(snapshot.SizeSummary),
            CommitActivities = [.. snapshot.CommitActivities.Select(MapCommitActivity)],
            HasEnrichment = snapshot.HasEnrichment
        };
    }

    /// <inheritdoc />
    public bool TryMap(CachedPullRequestAnalysisSnapshot snapshot, out PullRequestAnalysisSnapshot mappedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        mappedSnapshot = null!;

        if (snapshot.Activities is null ||
            snapshot.CorrectionCommits is null ||
            snapshot.CommitActivities is null ||
            snapshot.SizeSummary is null)
        {
            return false;
        }

        var activities = snapshot.Activities.Select(MapActivity).ToArray();
        var correctionCommits = snapshot.CorrectionCommits.Select(MapCommit).ToArray();
        var commitActivities = snapshot.CommitActivities.Select(MapCommitActivity).ToArray();

        mappedSnapshot = new PullRequestAnalysisSnapshot(
            activities,
            correctionCommits,
            MapSizeSummary(snapshot.SizeSummary),
            commitActivities,
            snapshot.HasEnrichment);

        return mappedSnapshot.Activities.All(static activity => activity is not null) &&
               mappedSnapshot.CorrectionCommits.All(static commit => commit is not null) &&
               mappedSnapshot.CommitActivities.All(static activity => activity is not null);
    }

    private static CachedPullRequestActivity MapActivity(PullRequestActivity activity)
    {
        return new CachedPullRequestActivity
        {
            ActivityDate = activity.ActivityDate,
            MergeDate = activity.MergeDate,
            Actor = activity.Actor.HasValue ? MapIdentity(activity.Actor.Value) : null,
            Comment = activity.Comment is null ? null : new CachedActivityComment
            {
                User = MapIdentity(activity.Comment.User),
                Date = activity.Comment.Date
            },
            Approval = activity.Approval is null ? null : new CachedActivityApproval
            {
                User = MapIdentity(activity.Approval.User),
                Date = activity.Approval.Date
            }
        };
    }

    private static PullRequestActivity MapActivity(CachedPullRequestActivity activity)
    {
        return new PullRequestActivity(
            activity.ActivityDate,
            activity.MergeDate,
            activity.Actor is null ? null : MapIdentity(activity.Actor),
            activity.Comment is null ? null : new ActivityComment(MapIdentity(activity.Comment.User), activity.Comment.Date),
            activity.Approval is null ? null : new ActivityApproval(MapIdentity(activity.Approval.User), activity.Approval.Date));
    }

    private static CachedPullRequestCommitInfo MapCommit(PullRequestCommitInfo commit)
    {
        return new CachedPullRequestCommitInfo
        {
            Hash = commit.Hash,
            Date = commit.Date,
            Message = commit.Message
        };
    }

    private static PullRequestCommitInfo MapCommit(CachedPullRequestCommitInfo commit) =>
        new(commit.Hash, commit.Date, commit.Message);

    private static CachedDeveloperCommitActivity MapCommitActivity(DeveloperCommitActivity activity)
    {
        return new CachedDeveloperCommitActivity
        {
            Repository = activity.Repository,
            RepositorySlug = activity.RepositorySlug,
            PullRequestId = activity.PullRequestId.Value,
            CommitHash = activity.CommitHash,
            Message = activity.Message,
            Date = activity.Date,
            SizeSummary = MapSizeSummary(activity.SizeSummary)
        };
    }

    private static DeveloperCommitActivity MapCommitActivity(CachedDeveloperCommitActivity activity)
    {
        return new DeveloperCommitActivity(
            activity.Repository,
            activity.RepositorySlug,
            new PullRequestId(activity.PullRequestId),
            activity.CommitHash,
            activity.Message,
            activity.Date,
            MapSizeSummary(activity.SizeSummary));
    }

    private static CachedDeveloperIdentity MapIdentity(DeveloperIdentity identity)
    {
        return new CachedDeveloperIdentity
        {
            Uuid = identity.Uuid?.Value,
            DisplayName = identity.DisplayName.Value
        };
    }

    private static DeveloperIdentity MapIdentity(CachedDeveloperIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new DeveloperIdentity(
            string.IsNullOrWhiteSpace(identity.Uuid) ? null : new UserUuid(identity.Uuid),
            new DisplayName(identity.DisplayName));
    }

    private static CachedPullRequestSizeSummary MapSizeSummary(PullRequestSizeSummary summary)
    {
        return new CachedPullRequestSizeSummary
        {
            FilesChanged = summary.FilesChanged,
            LinesAdded = summary.LinesAdded,
            LinesRemoved = summary.LinesRemoved
        };
    }

    private static PullRequestSizeSummary MapSizeSummary(CachedPullRequestSizeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new PullRequestSizeSummary(summary.FilesChanged, summary.LinesAdded, summary.LinesRemoved);
    }
}

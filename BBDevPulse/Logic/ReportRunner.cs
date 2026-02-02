using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Logic;

/// <summary>
/// Coordinates Bitbucket data collection and report generation.
/// </summary>
public sealed class ReportRunner : IReportRunner
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRunner"/> class.
    /// </summary>
    /// <param name="client">Bitbucket API client.</param>
    /// <param name="presenter">Report presenter.</param>
    /// <param name="options">Bitbucket options.</param>
    public ReportRunner(
        IBitbucketClient client,
        IReportPresenter presenter,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _presenter = presenter;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var filterDate = DateTimeOffset.UtcNow.AddDays(-_options.Days);
        var workspace = new Workspace(_options.Workspace);
        var repoNameFilter = new RepoNameFilter(_options.RepoNameFilter);
        var repoNameList = (_options.RepoNameList ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => new RepoName(entry))
            .ToList();
        var repoSearchMode = _options.RepoSearchMode;
        var prTimeFilterMode = _options.PrTimeFilterMode;
        var branchNameList = (_options.BranchNameList ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => new BranchName(entry))
            .ToList();
        var reports = new List<PullRequestReport>();
        var developerStats = new Dictionary<DeveloperKey, DeveloperStats>();

        await _presenter.AnnounceAuthAsync(
                _client.GetCurrentUserAsync,
                cancellationToken)
            .ConfigureAwait(false);

        var repositories = await _presenter.FetchRepositoriesAsync(
                (onPage, token) => _client.GetRepositoriesAsync(workspace, onPage, token),
                cancellationToken)
            .ConfigureAwait(false);

        var filteredRepos = repositories
            .Where(repo => repo.MatchesFilter(repoSearchMode, repoNameFilter, repoNameList))
            .ToList();

        _presenter.RenderRepositoryTable(filteredRepos, repoSearchMode, repoNameFilter, repoNameList);
        _presenter.RenderBranchFilterInfo(branchNameList);

        await _presenter.AnalyzeRepositoriesAsync(filteredRepos, async (repo, token) =>
        {
            token.ThrowIfCancellationRequested();

            await foreach (var pr in _client.GetPullRequestsAsync(
                               workspace,
                               repo.Slug,
                               pr => pr.ShouldStopByTimeFilter(filterDate, prTimeFilterMode),
                               token).ConfigureAwait(false))
            {
                if (!pr.MatchesBranchFilter(branchNameList))
                {
                    continue;
                }
                
                var lastActivity = pr.CreatedOn;
                var authorIdentity = BuildDeveloperKey(pr.Author);
                var shouldCalculateTtfr = pr.CreatedOn >= filterDate;
                DateTimeOffset? firstReactionOn = null;
                DateTimeOffset? mergedOnFromActivity = null;
                var participants = new Dictionary<string, DeveloperIdentity>(StringComparer.OrdinalIgnoreCase);
                var commentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var totalComments = 0;
                var approvalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                await foreach (var activity in _client.GetPullRequestActivityAsync(
                                   workspace,
                                   repo.Slug,
                                   pr.Id,
                                   activity =>
                                   {
                                       var activityDate = activity.ActivityDate;
                                       return activityDate.HasValue && activityDate.Value < filterDate;
                                   },
                                   token).ConfigureAwait(false))
                {
                    var activityDate = activity.ActivityDate;
                    if (activityDate.HasValue && activityDate.Value > lastActivity)
                    {
                        lastActivity = activityDate.Value;
                    }

                    if (activity.MergeDate.HasValue)
                    {
                        var mergeDate = activity.MergeDate.Value;
                        if (!mergedOnFromActivity.HasValue || mergeDate < mergedOnFromActivity.Value)
                        {
                            mergedOnFromActivity = mergeDate;
                        }
                    }

                    var activityUser = activity.Actor;
                    if (activityUser.HasValue)
                    {
                        AddParticipant(participants, activityUser.Value);

                        if (activity.Comment is not null)
                        {
                            totalComments++;
                            if (activity.Comment.Date >= filterDate)
                            {
                                var commentUser = activity.Comment.User;
                                var commentKey = commentUser.Uuid?.Value ?? commentUser.DisplayName.Value;
                                commentCounts[commentKey] = commentCounts.GetValueOrDefault(commentKey) + 1;
                                AddParticipant(participants, commentUser);
                            }

                            if (shouldCalculateTtfr &&
                                activity.Comment.Date >= pr.CreatedOn &&
                                (!authorIdentity.HasValue ||
                                 !authorIdentity.Value.IsSameIdentity(activity.Comment.User)))
                            {
                                if (!firstReactionOn.HasValue || activity.Comment.Date < firstReactionOn.Value)
                                {
                                    firstReactionOn = activity.Comment.Date;
                                }
                            }
                        }

                        if (activity.Approval is not null)
                        {
                            if (activity.Approval.Date >= filterDate)
                            {
                                var approvalUser = activity.Approval.User;
                                var approvalKey = approvalUser.Uuid?.Value ?? approvalUser.DisplayName.Value;
                                approvalCounts[approvalKey] = approvalCounts.GetValueOrDefault(approvalKey) + 1;
                                AddParticipant(participants, approvalUser);
                            }

                            if (shouldCalculateTtfr &&
                                activity.Approval.Date >= pr.CreatedOn &&
                                (!authorIdentity.HasValue ||
                                 !authorIdentity.Value.IsSameIdentity(activity.Approval.User)))
                            {
                                if (!firstReactionOn.HasValue || activity.Approval.Date < firstReactionOn.Value)
                                {
                                    firstReactionOn = activity.Approval.Date;
                                }
                            }
                        }
                    }
                }

                var matchesFilter = pr.CreatedOn >= filterDate || lastActivity >= filterDate;
                if (!matchesFilter)
                {
                    continue;
                }

                if (authorIdentity.HasValue)
                {
                    participants[authorIdentity.Value.Uuid?.Value ?? authorIdentity.Value.DisplayName.Value] =
                        authorIdentity.Value;
                }

                var mergedOnResolved = mergedOnFromActivity ?? pr.MergedOn;
                if (authorIdentity.HasValue)
                {
                    if (pr.CreatedOn >= filterDate)
                    {
                        GetOrAddDeveloper(developerStats, authorIdentity.Value).PrsOpenedSince++;
                    }

                    if (mergedOnResolved.HasValue && mergedOnResolved.Value >= filterDate)
                    {
                        GetOrAddDeveloper(developerStats, authorIdentity.Value).PrsMergedAfter++;
                    }
                }

                foreach (var entry in commentCounts)
                {
                    if (participants.TryGetValue(entry.Key, out var participant))
                    {
                        GetOrAddDeveloper(developerStats, participant).CommentsAfter += entry.Value;
                    }
                }

                foreach (var entry in approvalCounts)
                {
                    if (participants.TryGetValue(entry.Key, out var participant))
                    {
                        GetOrAddDeveloper(developerStats, participant).ApprovalsAfter += entry.Value;
                    }
                }

                reports.Add(new PullRequestReport(
                    string.IsNullOrWhiteSpace(repo.Name.Value) ? repo.Slug.Value : repo.Name.Value,
                    pr.Author?.DisplayName.Value ?? "unknown",
                    pr.Destination?.Branch?.Name ?? "-",
                    pr.CreatedOn,
                    lastActivity,
                    mergedOnResolved,
                    pr.ResolveRejectedOn(),
                    pr.State,
                    pr.Id,
                    totalComments,
                    firstReactionOn
                ));
            }
        }, cancellationToken).ConfigureAwait(false);

        var sortedReports = reports
            .OrderBy(r => r.CreatedOn)
            .ToList();

        _presenter.RenderPullRequestTable(sortedReports, filterDate);
        _presenter.RenderMergeTimeStats(sortedReports);
        _presenter.RenderTtfrStats(sortedReports);
        _presenter.RenderDeveloperStatsTable(developerStats, filterDate);
    }

    private static DeveloperIdentity? BuildDeveloperKey(User? user)
    {
        if (user is null)
        {
            return null;
        }

        return new DeveloperIdentity(user.Uuid, user.DisplayName);
    }

    private static DeveloperStats GetOrAddDeveloper(
        Dictionary<DeveloperKey, DeveloperStats> stats,
        DeveloperIdentity identity)
    {
        var key = DeveloperKey.FromIdentity(identity);
        if (stats.TryGetValue(key, out var existing))
        {
            if (!string.IsNullOrWhiteSpace(identity.DisplayName.Value))
            {
                existing.DisplayName = identity.DisplayName;
            }

            return existing;
        }

        var created = new DeveloperStats(identity.DisplayName);
        stats[key] = created;
        return created;
    }

    private static void AddParticipant(
        Dictionary<string, DeveloperIdentity> participants,
        DeveloperIdentity identity)
    {
        var key = identity.Uuid?.Value ?? identity.DisplayName.Value;
        if (!participants.ContainsKey(key))
        {
            participants[key] = identity;
        }
    }

    private readonly IBitbucketClient _client;
    private readonly IReportPresenter _presenter;
    private readonly BitbucketOptions _options;
}

using System.Text.Json;

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
        var developerStats = new Dictionary<string, DeveloperStats>(StringComparer.OrdinalIgnoreCase);

        await _presenter.AnnounceAuthAsync(
                _client.GetCurrentUserAsync,
                cancellationToken)
            .ConfigureAwait(false);

        var repositories = await _presenter.FetchRepositoriesAsync(
                (onPage, token) => _client.GetRepositoriesAsync(workspace, onPage, token),
                cancellationToken)
            .ConfigureAwait(false);

        var filteredRepos = repositories
            .Where(repo => RepoMatchesFilter(repo, repoSearchMode, repoNameFilter, repoNameList))
            .ToList();

        _presenter.RenderRepositoryTable(filteredRepos, repoSearchMode, repoNameFilter, repoNameList);
        _presenter.RenderBranchFilterInfo(branchNameList);

        await _presenter.AnalyzeRepositoriesAsync(filteredRepos, async (repo, token) =>
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(repo.Slug.Value))
            {
                return;
            }

            await foreach (var pr in _client.GetPullRequestsAsync(
                               workspace,
                               repo.Slug,
                               pr => ShouldStopByTimeFilter(pr, filterDate, prTimeFilterMode),
                               token))
            {
                if (!BranchMatchesFilter(pr.Destination?.Branch?.Name, branchNameList))
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
                                       var activityDate = TryGetActivityDate(activity);
                                       return activityDate.HasValue && activityDate.Value < filterDate;
                                   },
                                   token))
                {
                    var activityDate = TryGetActivityDate(activity);
                    if (activityDate.HasValue && activityDate.Value > lastActivity)
                    {
                        lastActivity = activityDate.Value;
                    }

                    if (TryGetMergeDate(activity, out var mergeDate))
                    {
                        if (!mergedOnFromActivity.HasValue || mergeDate < mergedOnFromActivity.Value)
                        {
                            mergedOnFromActivity = mergeDate;
                        }
                    }

                    var activityUser = TryGetActivityUser(activity);
                    if (activityUser.HasValue)
                    {
                        AddParticipant(participants, activityUser.Value);

                        if (TryGetCommentInfo(activity, out var commentUser, out var commentDate))
                        {
                            totalComments++;
                            if (commentDate >= filterDate)
                            {
                                var commentKey = commentUser.Uuid?.Value ?? commentUser.DisplayName.Value;
                                commentCounts[commentKey] = commentCounts.GetValueOrDefault(commentKey) + 1;
                                AddParticipant(participants, commentUser);
                            }

                            if (shouldCalculateTtfr &&
                                commentDate >= pr.CreatedOn &&
                                (!authorIdentity.HasValue ||
                                 !IsSameIdentity(authorIdentity.Value, commentUser)))
                            {
                                if (!firstReactionOn.HasValue || commentDate < firstReactionOn.Value)
                                {
                                    firstReactionOn = commentDate;
                                }
                            }
                        }

                        if (TryGetApprovalInfo(activity, out var approvalUser, out var approvalDate))
                        {
                            if (approvalDate >= filterDate)
                            {
                                var approvalKey = approvalUser.Uuid?.Value ?? approvalUser.DisplayName.Value;
                                approvalCounts[approvalKey] = approvalCounts.GetValueOrDefault(approvalKey) + 1;
                                AddParticipant(participants, approvalUser);
                            }

                            if (shouldCalculateTtfr &&
                                approvalDate >= pr.CreatedOn &&
                                (!authorIdentity.HasValue ||
                                 !IsSameIdentity(authorIdentity.Value, approvalUser)))
                            {
                                if (!firstReactionOn.HasValue || approvalDate < firstReactionOn.Value)
                                {
                                    firstReactionOn = approvalDate;
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
                    ResolveRejectedOn(pr),
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

    private static bool ShouldStopByTimeFilter(
        PullRequest pr,
        DateTimeOffset filterDate,
        PrTimeFilterMode filterMode)
    {
        return filterMode switch
        {
            PrTimeFilterMode.CreatedOnOnly => pr.CreatedOn < filterDate,
            PrTimeFilterMode.LastKnownUpdateAndCreated => throw new NotImplementedException(),
            _ =>
                            (pr.UpdatedOn ?? pr.CreatedOn) < filterDate &&
                            pr.CreatedOn < filterDate
        };
    }

    private static DateTimeOffset? TryGetActivityDate(PullRequestActivity activity)
    {
        var payload = activity.Payload;
        if (TryGetDate(payload, out var date, "comment", "created_on"))
        {
            return date;
        }

        if (TryGetDate(payload, out date, "approval", "date"))
        {
            return date;
        }

        if (TryGetDate(payload, out date, "approval", "approved_on"))
        {
            return date;
        }

        if (TryGetDate(payload, out date, "update", "date"))
        {
            return date;
        }

        if (TryGetDate(payload, out date, "pullrequest", "updated_on"))
        {
            return date;
        }

        if (TryGetDate(payload, out date, "pullrequest", "created_on"))
        {
            return date;
        }

        if (TryGetDate(payload, out date, "created_on"))
        {
            return date;
        }

        if (TryGetDate(payload, out date, "date"))
        {
            return date;
        }

        return TryGetDate(payload, out date, "updated_on") ? date : null;
    }

    private static bool TryGetMergeDate(PullRequestActivity activity, out DateTimeOffset date)
    {
        var payload = activity.Payload;
        if (TryGetString(payload, out var state, "pullrequest", "state") &&
            string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDate(payload, out date, "pullrequest", "merged_on"))
            {
                return true;
            }

            if (TryGetDate(payload, out date, "pullrequest", "updated_on"))
            {
                return true;
            }

            if (TryGetDate(payload, out date, "date"))
            {
                return true;
            }

            if (TryGetDate(payload, out date, "created_on"))
            {
                return true;
            }
        }

        if (TryGetString(payload, out state, "update", "state") &&
            string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDate(payload, out date, "update", "date"))
            {
                return true;
            }

            if (TryGetDate(payload, out date, "date"))
            {
                return true;
            }
        }

        if (TryGetDate(payload, out date, "merge", "date"))
        {
            return true;
        }

        if (TryGetDate(payload, out date, "merge", "created_on"))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static bool TryGetCommentInfo(PullRequestActivity activity, out DeveloperIdentity user, out DateTimeOffset date)
    {
        var payload = activity.Payload;
        if (TryGetCommentInfoForPath(payload, out user, out date, "comment"))
        {
            return true;
        }

        if (TryGetCommentInfoForPath(payload, out user, out date, "pullrequest_comment"))
        {
            return true;
        }

        if (TryGetCommentInfoForPath(payload, out user, out date, "pull_request_comment"))
        {
            return true;
        }

        user = default;
        date = default;
        return false;
    }

    private static bool TryGetCommentInfoForPath(
        JsonElement activity,
        out DeveloperIdentity user,
        out DateTimeOffset date,
        params string[] path)
    {
        if (TryGetUser(activity, out user, [.. path, "user"]))
        {
            if (TryGetDate(activity, out date, [.. path, "created_on"]) ||
                TryGetDate(activity, out date, [.. path, "updated_on"]) ||
                TryGetDate(activity, out date, "date") ||
                TryGetDate(activity, out date, "created_on"))
            {
                return true;
            }
        }

        user = default;
        date = default;
        return false;
    }

    private static bool TryGetApprovalInfo(PullRequestActivity activity, out DeveloperIdentity user, out DateTimeOffset date)
    {
        var payload = activity.Payload;
        if (TryGetUser(payload, out user, "approval", "user") &&
            (TryGetDate(payload, out date, "approval", "date") ||
             TryGetDate(payload, out date, "approval", "approved_on") ||
             TryGetDate(payload, out date, "date")))
        {
            return true;
        }

        user = default;
        date = default;
        return false;
    }

    private static DeveloperIdentity? TryGetActivityUser(PullRequestActivity activity)
    {
        var payload = activity.Payload;
        if (TryGetUser(payload, out var user, "comment", "user"))
        {
            return user;
        }

        if (TryGetUser(payload, out user, "approval", "user"))
        {
            return user;
        }

        if (TryGetUser(payload, out user, "update", "author"))
        {
            return user;
        }

        if (TryGetUser(payload, out user, "update", "user"))
        {
            return user;
        }

        if (TryGetUser(payload, out user, "pullrequest", "author"))
        {
            return user;
        }

        if (TryGetUser(payload, out user, "actor"))
        {
            return user;
        }

        return TryGetUser(payload, out user, "user") ? user : null;
    }

    private static bool TryGetString(JsonElement element, out string value, params string[] path)
    {
        if (TryGetNestedProperty(element, out var stringElement, path) &&
            stringElement.ValueKind == JsonValueKind.String)
        {
            value = stringElement.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetUser(JsonElement element, out DeveloperIdentity user, params string[] path)
    {
        if (TryGetNestedProperty(element, out var userElement, path))
        {
            var displayName = GetString(userElement, "display_name") ??
                GetString(userElement, "nickname") ??
                GetString(userElement, "username");
            var uuid = GetString(userElement, "uuid");

            if (!string.IsNullOrWhiteSpace(displayName) || !string.IsNullOrWhiteSpace(uuid))
            {
                var resolvedName = !string.IsNullOrWhiteSpace(displayName)
                    ? displayName
                    : uuid ?? "unknown";
                var resolvedUuid = string.IsNullOrWhiteSpace(uuid) ? null : new UserUuid(uuid);
                user = new DeveloperIdentity(
                    resolvedUuid,
                    new DisplayName(resolvedName));
                return true;
            }
        }

        user = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static bool TryGetDate(JsonElement element, out DateTimeOffset date, params string[] path)
    {
        if (TryGetNestedProperty(element, out var dateElement, path) &&
            dateElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(dateElement.GetString(), out date))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static bool TryGetNestedProperty(JsonElement element, out JsonElement result, params string[] path)
    {
        result = element;
        foreach (var segment in path)
        {
            if (result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty(segment, out var next))
            {
                return false;
            }

            result = next;
        }

        return true;
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
        Dictionary<string, DeveloperStats> stats,
        DeveloperIdentity identity)
    {
        var key = identity.Uuid?.Value ?? identity.DisplayName.Value;
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

    private static bool IsSameIdentity(DeveloperIdentity left, DeveloperIdentity right)
    {
        if (left.Uuid is not null && right.Uuid is not null)
        {
            return string.Equals(left.Uuid.Value, right.Uuid.Value, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left.DisplayName.Value, right.DisplayName.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ResolveRejectedOn(PullRequest pr)
    {
        return pr.State is not PullRequestState.Declined and
            not PullRequestState.Superseded
            ? null
            : pr.ClosedOn ?? pr.UpdatedOn;
    }

    private static bool RepoMatchesFilter(
        Repository repo,
        RepoSearchMode searchMode,
        RepoNameFilter filter,
        List<RepoName> repoList)
    {
        var name = repo.Name.Value;
        var slug = repo.Slug.Value;
        var filterValue = filter.Value;

        return searchMode switch
        {
            RepoSearchMode.FilterFromTheList => repoList.Count == 0 ||
                repoList.Any(entry =>
                    name.Equals(entry.Value, StringComparison.OrdinalIgnoreCase) ||
                    slug.Equals(entry.Value, StringComparison.OrdinalIgnoreCase)),
            RepoSearchMode.SearchByFilter => throw new NotImplementedException(),
            _ => string.IsNullOrWhiteSpace(filterValue) ||
                            name.Contains(filterValue, StringComparison.OrdinalIgnoreCase) ||
                            slug.Contains(filterValue, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool BranchMatchesFilter(string? targetBranch, List<BranchName> branchList)
    {
        if (branchList.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(targetBranch))
        {
            return false;
        }

        return branchList.Any(branch =>
            targetBranch.Equals(branch.Value, StringComparison.OrdinalIgnoreCase));
    }

    private readonly IBitbucketClient _client;
    private readonly IReportPresenter _presenter;
    private readonly BitbucketOptions _options;
}

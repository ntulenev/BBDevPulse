using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Logic;

/// <summary>
/// Pull request analysis implementation.
/// </summary>
internal sealed class PullRequestAnalyzer : IPullRequestAnalyzer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestAnalyzer"/> class.
    /// </summary>
    /// <param name="client">Bitbucket API client.</param>
    /// <param name="activityAnalyzer">Activity analyzer.</param>
    /// <param name="options">Bitbucket options.</param>
    public PullRequestAnalyzer(
        IBitbucketClient client,
        IActivityAnalyzer activityAnalyzer,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(activityAnalyzer);
        ArgumentNullException.ThrowIfNull(options);

        var maxConcurrentPullRequests = options.Value.PullRequestConcurrency;
        _client = client;
        _activityAnalyzer = activityAnalyzer;
        _maxConcurrentPullRequests = maxConcurrentPullRequests;
    }

    /// <inheritdoc />
    public async Task AnalyzeAsync(
        Repository repo,
        ReportData reportData,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(reportData);

        var parameters = reportData.Parameters;
        var workspace = parameters.Workspace;
        var filterDate = parameters.FilterDate;
        var prTimeFilterMode = parameters.PrTimeFilterMode;
        var branchNameList = parameters.BranchNameList;

        using var semaphore = new SemaphoreSlim(_maxConcurrentPullRequests, _maxConcurrentPullRequests);
        var analysisTasks = new List<Task>();

        await foreach (var pr in _client.GetPullRequestsAsync(
                           workspace,
                           repo.Slug,
                           pullRequest => pullRequest.ShouldStopByTimeFilter(filterDate, prTimeFilterMode),
                           cancellationToken).ConfigureAwait(false))
        {
            if (!pr.MatchesBranchFilter(branchNameList))
            {
                continue;
            }

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            analysisTasks.Add(AnalyzePullRequestWithSemaphoreAsync(
                repo,
                reportData,
                pr,
                semaphore,
                cancellationToken));
        }

        await Task.WhenAll(analysisTasks).ConfigureAwait(false);
    }

    private async Task AnalyzePullRequestWithSemaphoreAsync(
        Repository repo,
        ReportData reportData,
        PullRequest pr,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            await AnalyzePullRequestAsync(repo, reportData, pr, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = semaphore.Release();
        }
    }

    private async Task AnalyzePullRequestAsync(
        Repository repo,
        ReportData reportData,
        PullRequest pr,
        CancellationToken cancellationToken)
    {
        var parameters = reportData.Parameters;
        var filterDate = parameters.FilterDate;
        var workspace = parameters.Workspace;

        var authorIdentity = pr.Author?.ToDeveloperIdentity();
        var includeAuthoredPullRequest = !parameters.HasTeamFilter ||
            (authorIdentity.HasValue && reportData.IsDeveloperIncluded(authorIdentity.Value));
        var analysis = new ActivityAnalysisState(
            pr.CreatedOn,
            authorIdentity,
            pr.ShouldCalculateTtfr(filterDate));

        await foreach (var activity in _client.GetPullRequestActivityAsync(
                           workspace,
                           repo.Slug,
                           pr.Id,
                           pullRequestActivity => pullRequestActivity.IsBefore(filterDate),
                           cancellationToken).ConfigureAwait(false))
        {
            if (parameters.HasTeamFilter && HasIncludedTeamActivity(activity, reportData))
            {
                analysis.HasIncludedTeamActivity = true;
            }

            _activityAnalyzer.Analyze(analysis, activity, filterDate);
        }

        var matchesFilter = pr.CreatedOn >= filterDate || analysis.LastActivity >= filterDate;
        if (!matchesFilter)
        {
            return;
        }

        var hasTeamActivity = parameters.HasTeamFilter && analysis.HasIncludedTeamActivity;
        var shouldDisplayPullRequest = includeAuthoredPullRequest || hasTeamActivity;
        if (!shouldDisplayPullRequest)
        {
            return;
        }

        if (authorIdentity.HasValue)
        {
            analysis.Participants[authorIdentity.Value.ToKey()] = authorIdentity.Value;
        }

        var mergedOnResolved = analysis.MergedOnFromActivity ?? pr.MergedOn;
        var corrections = 0;
        var sizeSummary = PullRequestSizeSummary.Empty;
        if (shouldDisplayPullRequest)
        {
            corrections = await CountCorrectionsAsync(workspace, repo.Slug, pr.Id, pr.CreatedOn, cancellationToken)
                .ConfigureAwait(false);
            sizeSummary = await GetPullRequestSizeSafeAsync(workspace, repo.Slug, pr.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        lock (reportData)
        {
            if (authorIdentity.HasValue && includeAuthoredPullRequest)
            {
                var authorStats = reportData.GetOrAddDeveloper(authorIdentity.Value);
                if (pr.CreatedOn >= filterDate)
                {
                    authorStats.PrsOpenedSince++;
                }

                if (mergedOnResolved.HasValue && mergedOnResolved.Value >= filterDate)
                {
                    authorStats.PrsMergedAfter++;
                }

                authorStats.Corrections += corrections;
            }

            foreach (var entry in analysis.CommentCounts)
            {
                if (analysis.Participants.TryGetValue(entry.Key, out var participant) &&
                    reportData.IsDeveloperIncluded(participant))
                {
                    reportData.GetOrAddDeveloper(participant).CommentsAfter += entry.Value;
                }
            }

            foreach (var entry in analysis.ApprovalCounts)
            {
                if (analysis.Participants.TryGetValue(entry.Key, out var participant) &&
                    reportData.IsDeveloperIncluded(participant))
                {
                    reportData.GetOrAddDeveloper(participant).ApprovalsAfter += entry.Value;
                }
            }

            if (shouldDisplayPullRequest)
            {
                reportData.Reports.Add(new PullRequestReport(
                    string.IsNullOrWhiteSpace(repo.Name.Value) ? repo.Slug.Value : repo.Name.Value,
                    repo.Slug.Value,
                    pr.Author?.DisplayName.Value ?? "unknown",
                    pr.Destination?.Branch?.Name ?? "-",
                    pr.CreatedOn,
                    analysis.LastActivity,
                    mergedOnResolved,
                    pr.ResolveRejectedOn(),
                    pr.State,
                    pr.Id,
                    analysis.TotalComments,
                    corrections,
                    analysis.FirstReactionOn,
                sizeSummary.FilesChanged,
                sizeSummary.LinesAdded,
                sizeSummary.LinesRemoved,
                isActivityOnlyMatch: !includeAuthoredPullRequest));
            }
        }
    }

    private async Task<int> CountCorrectionsAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        DateTimeOffset createdOn,
        CancellationToken cancellationToken)
    {
        var corrections = 0;
        await foreach (var commitDate in _client.GetPullRequestCommitDatesAsync(
                           workspace,
                           repoSlug,
                           pullRequestId,
                           cancellationToken).ConfigureAwait(false))
        {
            if (commitDate > createdOn)
            {
                corrections++;
                continue;
            }

            break;
        }

        return corrections;
    }

    private static bool HasIncludedTeamActivity(PullRequestActivity activity, ReportData reportData)
    {
        if (activity.Actor.HasValue && reportData.IsDeveloperIncluded(activity.Actor.Value))
        {
            return true;
        }

        if (activity.Comment is not null && reportData.IsDeveloperIncluded(activity.Comment.User))
        {
            return true;
        }

        return activity.Approval is not null && reportData.IsDeveloperIncluded(activity.Approval.User);
    }

    private async Task<PullRequestSizeSummary> GetPullRequestSizeSafeAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetPullRequestSizeAsync(
                workspace,
                repoSlug,
                pullRequestId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return PullRequestSizeSummary.Empty;
        }
    }

    private readonly IBitbucketClient _client;
    private readonly IActivityAnalyzer _activityAnalyzer;
    private readonly int _maxConcurrentPullRequests;
}

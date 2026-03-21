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

            if (parameters.HasUpperBound &&
                pr.CreatedOn >= parameters.ToDateExclusive!.Value)
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
        var collectDeveloperDetails = parameters.ShowAllDetailsForDevelopers;

        var authorIdentity = pr.Author?.ToDeveloperIdentity();
        var includeAuthoredPullRequest = !parameters.HasTeamFilter ||
            (authorIdentity.HasValue && reportData.IsDeveloperIncluded(authorIdentity.Value));
        var analysis = new ActivityAnalysisState(
            pr.CreatedOn,
            authorIdentity,
            pr.IsCreatedInRange(parameters));
        var detailedComments = collectDeveloperDetails
            ? new List<(DeveloperIdentity User, DateTimeOffset Date)>()
            : null;
        var detailedApprovals = collectDeveloperDetails
            ? new List<(DeveloperIdentity User, DateTimeOffset Date)>()
            : null;

        await foreach (var activity in _client.GetPullRequestActivityAsync(
                           workspace,
                           repo.Slug,
                           pr.Id,
                           pullRequestActivity => pullRequestActivity.IsBefore(filterDate),
                           cancellationToken).ConfigureAwait(false))
        {
            if (parameters.HasTeamFilter &&
                parameters.IsInRange(activity.ActivityDate) &&
                HasIncludedTeamActivity(activity, reportData))
            {
                analysis.HasIncludedTeamActivity = true;
            }

            _activityAnalyzer.Analyze(analysis, activity, parameters);

            if (collectDeveloperDetails)
            {
                if (activity.Comment is not null && parameters.IsInRange(activity.Comment.Date))
                {
                    detailedComments!.Add((activity.Comment.User, activity.Comment.Date));
                }

                if (activity.Approval is not null && parameters.IsInRange(activity.Approval.Date))
                {
                    detailedApprovals!.Add((activity.Approval.User, activity.Approval.Date));
                }
            }
        }

        var matchesFilter = parameters.IsInRange(pr.CreatedOn) || analysis.HasActivityInRange;
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
        if (!parameters.IsInRange(mergedOnResolved))
        {
            mergedOnResolved = null;
        }

        var rejectedOn = pr.ResolveRejectedOn();
        if (!parameters.IsInRange(rejectedOn))
        {
            rejectedOn = null;
        }

        IReadOnlyList<PullRequestCommitInfo> correctionCommits = [];
        IReadOnlyList<DeveloperCommitActivity> correctionCommitActivities = [];
        var sizeSummary = PullRequestSizeSummary.Empty;
        if (shouldDisplayPullRequest)
        {
            correctionCommits = await GetCorrectionCommitsAsync(workspace, repo.Slug, pr.Id, pr.CreatedOn, parameters, cancellationToken)
                .ConfigureAwait(false);
            sizeSummary = await GetPullRequestSizeSafeAsync(workspace, repo.Slug, pr.Id, cancellationToken)
                .ConfigureAwait(false);
            if (collectDeveloperDetails && authorIdentity.HasValue && includeAuthoredPullRequest && correctionCommits.Count > 0)
            {
                correctionCommitActivities = await GetCommitActivitiesAsync(
                    repositoryName: string.IsNullOrWhiteSpace(repo.Name.Value) ? repo.Slug.Value : repo.Name.Value,
                    workspace,
                    repo.Slug,
                    pr.Id,
                    correctionCommits,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var corrections = correctionCommits.Count;
        var repositoryName = string.IsNullOrWhiteSpace(repo.Name.Value) ? repo.Slug.Value : repo.Name.Value;
        var pullRequestAuthor = pr.Author?.DisplayName.Value ?? "unknown";
        var reportEntry = shouldDisplayPullRequest
            ? new PullRequestReport(
                repositoryName,
                repo.Slug.Value,
                pullRequestAuthor,
                pr.Destination?.Branch?.Name ?? "-",
                pr.CreatedOn,
                analysis.LastActivity,
                mergedOnResolved,
                rejectedOn,
                pr.State,
                pr.Id,
                analysis.TotalComments,
                corrections,
                analysis.FirstReactionOn,
                sizeSummary.FilesChanged,
                sizeSummary.LinesAdded,
                sizeSummary.LinesRemoved,
                isActivityOnlyMatch: !includeAuthoredPullRequest)
            : null;

        lock (reportData)
        {
            if (authorIdentity.HasValue && includeAuthoredPullRequest)
            {
                var authorStats = reportData.GetOrAddDeveloper(authorIdentity.Value);
                if (parameters.IsInRange(pr.CreatedOn))
                {
                    authorStats.PrsOpenedSince++;
                }

                if (parameters.IsInRange(mergedOnResolved))
                {
                    authorStats.PrsMergedAfter++;
                }

                authorStats.Corrections += corrections;
                if (collectDeveloperDetails && reportEntry is not null)
                {
                    authorStats.AuthoredPullRequests.Add(reportEntry);
                    foreach (var commit in correctionCommitActivities)
                    {
                        authorStats.CommitActivities.Add(commit);
                    }
                }
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

            if (collectDeveloperDetails)
            {
                foreach (var detail in detailedComments!)
                {
                    if (reportData.IsDeveloperIncluded(detail.User))
                    {
                        reportData.GetOrAddDeveloper(detail.User).CommentActivities.Add(new DeveloperCommentActivity(
                            repositoryName,
                            repo.Slug.Value,
                            pr.Id,
                            pullRequestAuthor,
                            detail.Date));
                    }
                }

                foreach (var detail in detailedApprovals!)
                {
                    if (reportData.IsDeveloperIncluded(detail.User))
                    {
                        reportData.GetOrAddDeveloper(detail.User).ApprovalActivities.Add(new DeveloperApprovalActivity(
                            repositoryName,
                            repo.Slug.Value,
                            pr.Id,
                            pullRequestAuthor,
                            detail.Date));
                    }
                }
            }

            if (shouldDisplayPullRequest)
            {
                reportData.Reports.Add(reportEntry!);
            }
        }
    }

    private async Task<IReadOnlyList<PullRequestCommitInfo>> GetCorrectionCommitsAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        DateTimeOffset createdOn,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var correctionCommits = new List<PullRequestCommitInfo>();
            await foreach (var commit in _client.GetPullRequestCommitsAsync(
                               workspace,
                               repoSlug,
                               pullRequestId,
                               cancellationToken).ConfigureAwait(false))
            {
                if (commit.Date > createdOn)
                {
                    if (parameters.IsInRange(commit.Date))
                    {
                        correctionCommits.Add(commit);
                    }

                    continue;
                }

                break;
            }

            return correctionCommits;
        }
        catch (InvalidOperationException)
        {
            // Correction commits are supplemental data and should not fail the whole report.
            return [];
        }
    }

    private async Task<IReadOnlyList<DeveloperCommitActivity>> GetCommitActivitiesAsync(
        string repositoryName,
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        IReadOnlyList<PullRequestCommitInfo> correctionCommits,
        CancellationToken cancellationToken)
    {
        var activities = new List<DeveloperCommitActivity>(correctionCommits.Count);

        foreach (var commit in correctionCommits)
        {
            var sizeSummary = await GetCommitSizeSafeAsync(workspace, repoSlug, commit.Hash, cancellationToken)
                .ConfigureAwait(false);
            activities.Add(new DeveloperCommitActivity(
                repositoryName,
                repoSlug.Value,
                pullRequestId,
                commit.Hash,
                commit.Message,
                commit.Date,
                sizeSummary));
        }

        return activities;
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

    private async Task<PullRequestSizeSummary> GetCommitSizeSafeAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        string commitHash,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetCommitSizeAsync(
                workspace,
                repoSlug,
                commitHash,
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

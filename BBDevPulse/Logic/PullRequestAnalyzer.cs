using BBDevPulse.Abstractions;
using BBDevPulse.Models;

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
    public PullRequestAnalyzer(IBitbucketClient client, IActivityAnalyzer activityAnalyzer)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(activityAnalyzer);
        _client = client;
        _activityAnalyzer = activityAnalyzer;
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

            var authorIdentity = pr.Author?.ToDeveloperIdentity();
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
                _activityAnalyzer.Analyze(analysis, activity, filterDate);
            }

            var matchesFilter = pr.CreatedOn >= filterDate || analysis.LastActivity >= filterDate;
            if (!matchesFilter)
            {
                continue;
            }

            if (authorIdentity.HasValue)
            {
                analysis.Participants[authorIdentity.Value.ToKey()] = authorIdentity.Value;
            }

            var mergedOnResolved = analysis.MergedOnFromActivity ?? pr.MergedOn;
            if (authorIdentity.HasValue)
            {
                if (pr.CreatedOn >= filterDate)
                {
                    reportData.GetOrAddDeveloper(authorIdentity.Value).PrsOpenedSince++;
                }

                if (mergedOnResolved.HasValue && mergedOnResolved.Value >= filterDate)
                {
                    reportData.GetOrAddDeveloper(authorIdentity.Value).PrsMergedAfter++;
                }
            }

            foreach (var entry in analysis.CommentCounts)
            {
                if (analysis.Participants.TryGetValue(entry.Key, out var participant))
                {
                    reportData.GetOrAddDeveloper(participant).CommentsAfter += entry.Value;
                }
            }

            foreach (var entry in analysis.ApprovalCounts)
            {
                if (analysis.Participants.TryGetValue(entry.Key, out var participant))
                {
                    reportData.GetOrAddDeveloper(participant).ApprovalsAfter += entry.Value;
                }
            }

            reportData.Reports.Add(new PullRequestReport(
                string.IsNullOrWhiteSpace(repo.Name.Value) ? repo.Slug.Value : repo.Name.Value,
                pr.Author?.DisplayName.Value ?? "unknown",
                pr.Destination?.Branch?.Name ?? "-",
                pr.CreatedOn,
                analysis.LastActivity,
                mergedOnResolved,
                pr.ResolveRejectedOn(),
                pr.State,
                pr.Id,
                analysis.TotalComments,
                analysis.FirstReactionOn
            ));
        }
    }

    private readonly IBitbucketClient _client;
    private readonly IActivityAnalyzer _activityAnalyzer;

}

using System.Runtime.ExceptionServices;

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
    /// <param name="analysisCache">Pull request analysis cache.</param>
    /// <param name="cacheKeys">Creates cache fingerprints.</param>
    /// <param name="options">Bitbucket options.</param>
    public PullRequestAnalyzer(
        IBitbucketClient client,
        IActivityAnalyzer activityAnalyzer,
        IPullRequestAnalysisCache analysisCache,
        IPullRequestAnalysisCacheKeyFactory cacheKeys,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(activityAnalyzer);
        ArgumentNullException.ThrowIfNull(analysisCache);
        ArgumentNullException.ThrowIfNull(cacheKeys);
        ArgumentNullException.ThrowIfNull(options);

        var maxConcurrentPullRequests = options.Value.PullRequestConcurrency;
        _client = client;
        _activityAnalyzer = activityAnalyzer;
        _analysisCache = analysisCache;
        _cacheKeys = cacheKeys;
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
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var analysisTasks = new List<Task>();
        Exception? sourceError = null;
        Exception? handlerError = null;
        try
        {
            await foreach (var pr in _client.GetPullRequestsAsync(
                workspace,
                repo.Slug,
                pullRequest => pullRequest.ShouldStopByTimeFilter(filterDate, prTimeFilterMode),
                stop.Token).ConfigureAwait(false))
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

                await semaphore.WaitAsync(stop.Token).ConfigureAwait(false);
                analysisTasks.Add(AnalyzePullRequestWithSemaphoreAsync(
                    repo,
                    reportData,
                    pr,
                    semaphore,
                    exception =>
                    {
                        Interlocked.CompareExchange(ref handlerError, exception, null);
                        stop.Cancel();
                    },
                    stop.Token));
            }
        }
        catch (Exception exception)
        {
            sourceError = exception is OperationCanceledException &&
                !cancellationToken.IsCancellationRequested && handlerError is not null
                ? handlerError : exception;
            await stop.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await Task.WhenAll(analysisTasks).ConfigureAwait(false);
            }
            catch when (sourceError is not null)
            {
                // Keep the source failure after all active handlers have stopped.
            }
        }
        if (sourceError is not null)
        {
            ExceptionDispatchInfo.Capture(sourceError).Throw();
        }

    }

    private async Task AnalyzePullRequestWithSemaphoreAsync(
        Repository repo,
        ReportData reportData,
        PullRequest pr,
        SemaphoreSlim semaphore,
        Action<Exception> onError,
        CancellationToken cancellationToken)
    {
        try
        {
            await AnalyzePullRequestAsync(repo, reportData, pr, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            onError(exception);
            throw;
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
        var parametersFingerprint = _cacheKeys.BuildParametersFingerprint(parameters);
        var pullRequestFingerprint = _cacheKeys.BuildPullRequestFingerprint(pr);

        var observedIdentities = new List<DeveloperIdentity>();
        var authorIdentity = pr.Author?.ToDeveloperIdentity();
        observedIdentities.Add(authorIdentity ?? new DeveloperIdentity(null, new DisplayName("unknown")));
        if (!authorIdentity.HasValue || authorIdentity.Value.Uuid is null)
        {
            reportData.ApplyResult(PullRequestAnalysisResult.Skipped(workspace, repo.Slug, pr.Id, observedIdentities));
            return;
        }
        var includeAuthoredPullRequest = !parameters.HasTeamFilter ||
            (authorIdentity.HasValue && reportData.IsDeveloperIncluded(authorIdentity.Value));
        var cacheHit = TryReadAnalysisSnapshot(
            workspace,
            repo.Slug,
            pr.Id,
            pullRequestFingerprint,
            parametersFingerprint,
            out var snapshot);
        if (!cacheHit)
        {
            snapshot = await BuildActivitySnapshotAsync(
                workspace,
                repo.Slug,
                pr.Id,
                filterDate,
                cancellationToken).ConfigureAwait(false);
        }

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

        foreach (var activity in snapshot.Activities)
        {
            if (activity.Actor.HasValue)
            {
                observedIdentities.Add(activity.Actor.Value);
            }
            if (activity.Comment is not null)
            {
                observedIdentities.Add(activity.Comment.User);
            }
            if (activity.Approval is not null)
            {
                observedIdentities.Add(activity.Approval.User);
            }
            if ((activity.Comment is not null && activity.Comment.User.Uuid is null) ||
                (activity.Approval is not null && activity.Approval.User.Uuid is null) ||
                (activity.Actor.HasValue && activity.Actor.Value.Uuid is null &&
                 activity.Comment is null && activity.Approval is null))
            {
                continue;
            }
            if (parameters.HasTeamFilter &&
                parameters.IsInRange(activity.ActivityDate) &&
                HasIncludedTeamActivity(activity, reportData))
            {
                analysis.HasIncludedTeamActivity = true;
            }

            _activityAnalyzer.Analyze(analysis, activity, parameters);

            if (collectDeveloperDetails)
            {
                if (activity.Comment is not null && activity.Comment.User.Uuid is not null && parameters.IsInRange(activity.Comment.Date))
                {
                    detailedComments!.Add((activity.Comment.User, activity.Comment.Date));
                }

                if (activity.Approval is not null && activity.Approval.User.Uuid is not null && parameters.IsInRange(activity.Approval.Date))
                {
                    detailedApprovals!.Add((activity.Approval.User, activity.Approval.Date));
                }
            }
        }

        var matchesFilter = parameters.IsInRange(pr.CreatedOn) || analysis.HasActivityInRange;
        if (!matchesFilter)
        {
            reportData.ApplyResult(PullRequestAnalysisResult.Skipped(workspace, repo.Slug, pr.Id, observedIdentities));
            return;
        }

        var hasTeamActivity = parameters.HasTeamFilter && analysis.HasIncludedTeamActivity;
        var shouldDisplayPullRequest = includeAuthoredPullRequest || hasTeamActivity;
        if (!shouldDisplayPullRequest)
        {
            if (!cacheHit)
            {
                _analysisCache.Store(
                    workspace,
                    repo.Slug,
                    pr.Id,
                    pullRequestFingerprint,
                    parametersFingerprint,
                    snapshot);
            }

            reportData.ApplyResult(PullRequestAnalysisResult.Skipped(workspace, repo.Slug, pr.Id, observedIdentities));
            return;
        }

        if (!snapshot.HasCommits || !snapshot.HasSize ||
            (collectDeveloperDetails && snapshot.CommitActivities.Any(static activity => !activity.HasSizeData)))
        {
            snapshot = await EnrichAnalysisSnapshotAsync(
                snapshot,
                repo,
                pr,
                parameters,
                authorIdentity.HasValue,
                collectDeveloperDetails,
                cancellationToken).ConfigureAwait(false);

            _analysisCache.Store(
                workspace,
                repo.Slug,
                pr.Id,
                pullRequestFingerprint,
                parametersFingerprint,
                snapshot);
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

        var correctionCommits = snapshot.CorrectionCommits;
        IReadOnlyList<DeveloperCommitActivity> correctionCommitActivities = [];
        var sizeSummary = snapshot.SizeSummary;
        if (shouldDisplayPullRequest)
        {
            if (collectDeveloperDetails && authorIdentity.HasValue && includeAuthoredPullRequest && snapshot.CommitActivities.Count > 0)
            {
                correctionCommitActivities = snapshot.CommitActivities;
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
                isActivityOnlyMatch: !includeAuthoredPullRequest,
                hasSizeData: snapshot.HasSize,
                hasCorrectionsData: snapshot.HasCommits)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        reportData.ApplyResult(new PullRequestAnalysisResult(
            workspace, repo, pr, observedIdentities, authorIdentity, includeAuthoredPullRequest,
            reportEntry!, mergedOnResolved, corrections, snapshot.HasCommits, analysis,
            correctionCommitActivities, detailedComments, detailedApprovals));
    }

    private async Task<IReadOnlyList<PullRequestCommitInfo>?> GetCorrectionCommitsAsync(
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
                }
            }

            return correctionCommits;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Correction commits are supplemental data and should not fail the whole report.
            return null;
        }
    }

    private bool TryReadAnalysisSnapshot(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        string pullRequestFingerprint,
        string parametersFingerprint,
        out PullRequestAnalysisSnapshot snapshot)
    {
        return _analysisCache.TryGet(
            workspace,
            repoSlug,
            pullRequestId,
            pullRequestFingerprint,
            parametersFingerprint,
            out snapshot);
    }

    private async Task<PullRequestAnalysisSnapshot> BuildActivitySnapshotAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        DateTimeOffset filterDate,
        CancellationToken cancellationToken)
    {
        var activities = new List<PullRequestActivity>();

        await foreach (var activity in _client.GetPullRequestActivityAsync(
                           workspace,
                           repoSlug,
                           pullRequestId,
                           pullRequestActivity => pullRequestActivity.IsBefore(filterDate),
                           cancellationToken).ConfigureAwait(false))
        {
            activities.Add(activity);
        }

        return new PullRequestAnalysisSnapshot(
            activities,
            correctionCommits: [],
            sizeSummary: PullRequestSizeSummary.Empty,
            commitActivities: [],
            hasEnrichment: false,
            commitsAvailability: DataAvailability.NotRequested,
            sizeAvailability: DataAvailability.NotRequested);
    }

    private async Task<PullRequestAnalysisSnapshot> EnrichAnalysisSnapshotAsync(
        PullRequestAnalysisSnapshot snapshot,
        Repository repo,
        PullRequest pr,
        ReportParameters parameters,
        bool hasAuthorIdentity,
        bool collectDeveloperDetails,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var loadedCommits = snapshot.HasCommits ? snapshot.CorrectionCommits :
            await GetCorrectionCommitsAsync(parameters.Workspace, repo.Slug, pr.Id,
                pr.CreatedOn, parameters, cancellationToken).ConfigureAwait(false);
        var correctionCommits = loadedCommits ?? snapshot.CorrectionCommits;
        var loadedSize = snapshot.HasSize ? snapshot.SizeSummary :
            await GetPullRequestSizeSafeAsync(parameters.Workspace, repo.Slug, pr.Id,
                cancellationToken).ConfigureAwait(false);
        var sizeSummary = loadedSize ?? snapshot.SizeSummary;

        IReadOnlyList<DeveloperCommitActivity> commitActivities = [];
        if (collectDeveloperDetails && hasAuthorIdentity && loadedCommits is not null && correctionCommits.Count > 0)
        {
            commitActivities = await GetCommitActivitiesAsync(
                repositoryName: repo.DisplayName,
                parameters.Workspace,
                repo.Slug,
                pr.Id,
                correctionCommits,
                snapshot.CommitActivities,
                cancellationToken).ConfigureAwait(false);
        }

        return new PullRequestAnalysisSnapshot(
            snapshot.Activities,
            correctionCommits,
            sizeSummary,
            commitActivities,
            hasEnrichment: loadedCommits is not null && loadedSize is not null &&
                commitActivities.All(static activity => activity.HasSizeData),
            hasCommits: loadedCommits is not null,
            hasSize: loadedSize is not null,
            commitsAvailability: loadedCommits is null ? DataAvailability.Unavailable : DataAvailability.Available,
            sizeAvailability: loadedSize is null ? DataAvailability.Unavailable : DataAvailability.Available);
    }

    private async Task<IReadOnlyList<DeveloperCommitActivity>> GetCommitActivitiesAsync(
        string repositoryName,
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        IReadOnlyList<PullRequestCommitInfo> correctionCommits,
        IReadOnlyList<DeveloperCommitActivity> cachedActivities,
        CancellationToken cancellationToken)
    {
        var activities = new List<DeveloperCommitActivity>(correctionCommits.Count);
        var cachedSizes = new Dictionary<string, PullRequestSizeSummary>(StringComparer.Ordinal);
        foreach (var activity in cachedActivities)
        {
            if (activity.HasSizeData)
            {
                cachedSizes.TryAdd(activity.CommitHash, activity.SizeSummary);
            }
        }

        foreach (var commit in correctionCommits)
        {
            PullRequestSizeSummary? sizeSummary;
            if (cachedSizes.TryGetValue(commit.Hash, out var cachedSize))
            {
                sizeSummary = cachedSize;
            }
            else
            {
                sizeSummary = await GetCommitSizeSafeAsync(workspace, repoSlug, commit.Hash, cancellationToken)
                    .ConfigureAwait(false);
            }
            activities.Add(new DeveloperCommitActivity(
                repositoryName,
                repoSlug.Value,
                pullRequestId,
                commit.Hash,
                commit.Message,
                commit.Date,
                sizeSummary ?? PullRequestSizeSummary.Empty,
                sizeSummary is not null));
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

    private async Task<PullRequestSizeSummary?> GetPullRequestSizeSafeAsync(
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private async Task<PullRequestSizeSummary?> GetCommitSizeSafeAsync(
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private readonly IBitbucketClient _client;
    private readonly IActivityAnalyzer _activityAnalyzer;
    private readonly IPullRequestAnalysisCache _analysisCache;
    private readonly IPullRequestAnalysisCacheKeyFactory _cacheKeys;
    private readonly int _maxConcurrentPullRequests;
}

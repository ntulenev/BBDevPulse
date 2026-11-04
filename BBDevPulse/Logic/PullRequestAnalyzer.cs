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
        ReportAccumulator reportData,
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
        ReportAccumulator reportData,
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
        ReportAccumulator reportData,
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

        var evaluation = PullRequestResultBuilder.Evaluate(
            pr, parameters, snapshot, reportData.IsDeveloperIncluded, _activityAnalyzer);
        if (evaluation.Selection != PullRequestResultBuilder.Selection.Included)
        {
            if (evaluation.Selection == PullRequestResultBuilder.Selection.OutsideTeam && !cacheHit)
            {
                _analysisCache.Store(workspace, repo.Slug, pr.Id,
                    pullRequestFingerprint, parametersFingerprint, snapshot);
            }
            reportData.ApplyResult(PullRequestResultBuilder.Complete(repo, pr, parameters, snapshot, evaluation));
            return;
        }

        if (!snapshot.HasCommits || !snapshot.HasSize || !snapshot.SizeSummary.HasLineData ||
            (collectDeveloperDetails && snapshot.CommitActivities.Any(static activity => !activity.HasSizeData || !activity.SizeSummary.HasLineData)))
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

        cancellationToken.ThrowIfCancellationRequested();
        reportData.ApplyResult(PullRequestResultBuilder.Complete(repo, pr, parameters, snapshot, evaluation));
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
        var loadedSize = snapshot.HasSize && snapshot.SizeSummary.HasLineData ? snapshot.SizeSummary :
            await GetPullRequestSizeSafeAsync(parameters.Workspace, repo.Slug, pr.Id,
                cancellationToken).ConfigureAwait(false);
        var effectiveSize = loadedSize ?? (snapshot.HasSize ? snapshot.SizeSummary : (PullRequestSizeSummary?)null);
        var sizeSummary = effectiveSize ?? snapshot.SizeSummary;

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
            hasEnrichment: loadedCommits is not null && effectiveSize is not null && effectiveSize.Value.HasLineData &&
                commitActivities.All(static activity => activity.HasSizeData && activity.SizeSummary.HasLineData),
            hasCommits: loadedCommits is not null,
            hasSize: effectiveSize is not null,
            commitsAvailability: loadedCommits is null ? DataAvailability.Unavailable : DataAvailability.Available,
            sizeAvailability: effectiveSize is null ? DataAvailability.Unavailable :
                effectiveSize.Value.HasLineData ? DataAvailability.Available : DataAvailability.Incomplete);
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
            if (cachedSizes.TryGetValue(commit.Hash, out var cachedSize) && cachedSize.HasLineData)
            {
                sizeSummary = cachedSize;
            }
            else
            {
                sizeSummary = await GetCommitSizeSafeAsync(workspace, repoSlug, commit.Hash, cancellationToken)
                    .ConfigureAwait(false) ?? (cachedSizes.ContainsKey(commit.Hash) ? cachedSize : null);
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

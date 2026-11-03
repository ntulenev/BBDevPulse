using System.Runtime.CompilerServices;
using System.Text.Json;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;
using BBDevPulse.Transport;

using Microsoft.Extensions.Options;

namespace BBDevPulse.API;

/// <summary>
/// Bitbucket API client implementation.
/// </summary>
internal sealed class BitbucketClient : IBitbucketClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketClient"/> class.
    /// </summary>
    /// <param name="options">Bitbucket options.</param>
    /// <param name="reader">Reads individual and paginated Bitbucket responses.</param>
    /// <param name="uriBuilder">General URI builder.</param>
    /// <param name="repositoriesUriBuilder">Repository URI builder.</param>
    /// <param name="pullRequestsUriBuilder">Pull request URI builder.</param>
    /// <param name="pullRequestCommitRangeCache">Commit range cache.</param>
    /// <param name="mapper">Bitbucket response mapper.</param>
    public BitbucketClient(
        IOptions<BitbucketOptions> options,
        BitbucketResponseReader reader,
        IBitbucketUriBuilder uriBuilder,
        IRepositoriesUriBuilder repositoriesUriBuilder,
        IPullRequestsUriBuilder pullRequestsUriBuilder,
        IPullRequestCommitRangeCache pullRequestCommitRangeCache,
        IBitbucketMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(uriBuilder);
        ArgumentNullException.ThrowIfNull(repositoriesUriBuilder);
        ArgumentNullException.ThrowIfNull(pullRequestsUriBuilder);
        ArgumentNullException.ThrowIfNull(pullRequestCommitRangeCache);
        ArgumentNullException.ThrowIfNull(mapper);
        var optionsValue = options.Value;
        _options = optionsValue;
        _reader = reader;
        _uriBuilder = uriBuilder;
        _repositoriesUriBuilder = repositoriesUriBuilder;
        _pullRequestsUriBuilder = pullRequestsUriBuilder;
        _pullRequestCommitRangeCache = pullRequestCommitRangeCache;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<AuthUser> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var dto = await _reader.GetAsync<AuthUserDto>(new Uri("user", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);
        return _mapper.Map(dto);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Repository> GetRepositoriesAsync(
        Workspace workspace,
        Action<int>? onPage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var firstPage = _repositoriesUriBuilder.Build(workspace);

        await foreach (var repo in _reader.ReadAllAsync<RepositoryDto>(
            firstPage,
            onPage,
            cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return _mapper.Map(repo);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PullRequest> GetPullRequestsAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        Func<PullRequest, bool> shouldStop,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);
        ArgumentNullException.ThrowIfNull(shouldStop);
        var firstPage = _pullRequestsUriBuilder.Build(workspace, repoSlug);

        await foreach (var pr in _reader.ReadAllAsync<PullRequestDto>(
            firstPage,
            null,
            cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CachePullRequestCommitRange(workspace, repoSlug, pr);
            var model = _mapper.Map(pr);
            if (shouldStop(model))
            {
                break;
            }

            yield return model;
        }

    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PullRequestActivity> GetPullRequestActivityAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        Func<PullRequestActivity, bool> shouldStop,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);
        ArgumentNullException.ThrowIfNull(pullRequestId);
        ArgumentNullException.ThrowIfNull(shouldStop);
        var firstPage = _uriBuilder.BuildRelativeUri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests/{pullRequestId.Value}/activity" +
            $"?pagelen={_options.PageLength}&sort=-created_on",
            BitbucketFieldGroup.PullRequestActivity);

        await foreach (var activity in _reader.ReadAllAsync<JsonElement>(
            firstPage,
            null,
            cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = _mapper.Map(activity);
            if (shouldStop(model))
            {
                break;
            }

            yield return model;
        }

    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PullRequestCommitInfo> GetPullRequestCommitsAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);
        ArgumentNullException.ThrowIfNull(pullRequestId);
        var firstPage = _uriBuilder.BuildRelativeUri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests/{pullRequestId.Value}/commits" +
            $"?pagelen={_options.PageLength}&sort=-date",
            BitbucketFieldGroup.PullRequestCommit);

        await foreach (var commit in _reader.ReadAllAsync<PullRequestCommitDto>(
            firstPage,
            null,
            cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = commit.Summary?.Raw;
            if (commit.Date.HasValue &&
                !string.IsNullOrWhiteSpace(commit.Hash) &&
                !string.IsNullOrWhiteSpace(message))
            {
                yield return new PullRequestCommitInfo(commit.Hash, commit.Date.Value, message);
            }
        }
    }

    /// <inheritdoc />
    public async Task<PullRequestSizeSummary> GetCommitSizeAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        string commitHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitHash);

        var diffStatUri = _uriBuilder.BuildRelativeUri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/diffstat/{commitHash}" +
            $"?topic=true&pagelen={_options.PageLength}",
            BitbucketFieldGroup.PullRequestDiffStat);

        return await ReadPullRequestDiffStatAsync(diffStatUri, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PullRequestSizeSummary> GetPullRequestSizeAsync(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);
        ArgumentNullException.ThrowIfNull(pullRequestId);

        var sourceCommitHash = default(string);
        var destinationCommitHash = default(string);

        _ = _pullRequestCommitRangeCache.TryGet(
            workspace,
            repoSlug,
            pullRequestId.Value,
            out sourceCommitHash,
            out destinationCommitHash);

        if (string.IsNullOrWhiteSpace(sourceCommitHash) ||
            string.IsNullOrWhiteSpace(destinationCommitHash))
        {
            var pullRequestUri = _uriBuilder.BuildRelativeUri(
                $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests/{pullRequestId.Value}",
                BitbucketFieldGroup.PullRequestSizeReference);
            var pullRequestReference = await _reader.GetAsync<PullRequestSizeReferenceDto>(pullRequestUri, cancellationToken)
                .ConfigureAwait(false);

            sourceCommitHash = pullRequestReference.Source?.Commit?.Hash;
            destinationCommitHash = pullRequestReference.Destination?.Commit?.Hash;

            CachePullRequestCommitRange(
                workspace,
                repoSlug,
                pullRequestId.Value,
                sourceCommitHash,
                destinationCommitHash);
        }

        if (string.IsNullOrWhiteSpace(sourceCommitHash) ||
            string.IsNullOrWhiteSpace(destinationCommitHash))
        {
            throw new InvalidOperationException("Bitbucket pull request diffstat references are unavailable.");
        }

        var diffStatUri = _uriBuilder.BuildRelativeUri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/diffstat/" +
            $"{workspace.Value}/{repoSlug.Value}:{sourceCommitHash}..{destinationCommitHash}" +
            $"?topic=true&pagelen={_options.PageLength}",
            BitbucketFieldGroup.PullRequestDiffStat);

        return await ReadPullRequestDiffStatAsync(diffStatUri, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PullRequestSizeSummary> ReadPullRequestDiffStatAsync(
        Uri firstPageUri,
        CancellationToken cancellationToken)
    {
        var filesChanged = 0;
        var linesAdded = 0;
        var linesRemoved = 0;

        await foreach (var entry in _reader.ReadAllAsync<PullRequestDiffStatDto>(
            firstPageUri,
            null,
            cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            filesChanged++;
            linesAdded += entry.LinesAdded ?? 0;
            linesRemoved += entry.LinesRemoved ?? 0;
        }

        return new PullRequestSizeSummary(filesChanged, linesAdded, linesRemoved);
    }


    private void CachePullRequestCommitRange(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestDto pullRequest)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);
        ArgumentNullException.ThrowIfNull(pullRequest);

        CachePullRequestCommitRange(
            workspace,
            repoSlug,
            pullRequest.Id,
            pullRequest.Source?.Commit?.Hash,
            pullRequest.Destination?.Commit?.Hash);
    }

    private void CachePullRequestCommitRange(
        Workspace workspace,
        RepoSlug repoSlug,
        int pullRequestId,
        string? sourceCommitHash,
        string? destinationCommitHash)
    {
        if (string.IsNullOrWhiteSpace(sourceCommitHash) ||
            string.IsNullOrWhiteSpace(destinationCommitHash))
        {
            return;
        }

        _pullRequestCommitRangeCache.Store(
            workspace,
            repoSlug,
            pullRequestId,
            sourceCommitHash,
            destinationCommitHash);
    }


    private readonly BitbucketOptions _options;
    private readonly BitbucketResponseReader _reader;
    private readonly IBitbucketUriBuilder _uriBuilder;
    private readonly IRepositoriesUriBuilder _repositoriesUriBuilder;
    private readonly IPullRequestsUriBuilder _pullRequestsUriBuilder;
    private readonly IPullRequestCommitRangeCache _pullRequestCommitRangeCache;
    private readonly IBitbucketMapper _mapper;
}

using System.Runtime.CompilerServices;
using System.Globalization;
using System.Collections.Concurrent;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;
using BBDevPulse.Transport;

using System.Text.Json;

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
    /// <param name="httpClient">Configured HTTP client.</param>
    /// <param name="options">Bitbucket options.</param>
    /// <param name="activityMapper">Activity mapper.</param>
    public BitbucketClient(
        IOptions<BitbucketOptions> options,
        IBitbucketTransport transport,
        IPaginatorHelper paginatorHelper,
        IBitbucketMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(paginatorHelper);
        ArgumentNullException.ThrowIfNull(mapper);
        var optionsValue = options.Value;
        _options = optionsValue;
        _reportParameters = optionsValue.CreateReportParameters();
        _transport = transport;
        _paginatorHelper = paginatorHelper;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<AuthUser> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var dto = await GetAsync<AuthUserDto>(new Uri("user", UriKind.Relative), cancellationToken)
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
        var firstPage = new Uri(
            $"repositories/{workspace.Value}?pagelen={_options.PageLength}",
            UriKind.Relative);

        await foreach (var repo in _paginatorHelper.ReadAllAsync(
            firstPage,
            async (uri, ct) =>
            {
                var page = await GetAsync<PaginatedResponse<RepositoryDto>>(uri, ct)
                    .ConfigureAwait(false);
                return new PaginatedResult<RepositoryDto>(
                    page.Values ?? [],
                    GetNextUri(page.Next));
            },
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
        var firstPage = BuildPullRequestsUri(workspace, repoSlug);
#pragma warning disable CS0219 // Used in paginator
        var stop = false;
#pragma warning restore CS0219

        await foreach (var pr in _paginatorHelper.ReadAllAsync(
            firstPage,
            async (uri, ct) =>
            {
                var page = await GetAsync<PaginatedResponse<PullRequestDto>>(uri, ct)
                    .ConfigureAwait(false);
                return new PaginatedResult<PullRequestDto>(
                    page.Values ?? [],
                    GetNextUri(page.Next));
            },
            null,
            cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CachePullRequestCommitRange(workspace, repoSlug, pr);
            var model = _mapper.Map(pr);
            if (shouldStop(model))
            {
                stop = true;
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
        var firstPage = new Uri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests/{pullRequestId.Value}/activity" +
            $"?pagelen={_options.PageLength}&sort=-created_on",
            UriKind.Relative);
#pragma warning disable CS0219 // Used in paginator
        var stop = false;
#pragma warning restore CS0219

        await foreach (var activity in _paginatorHelper.ReadAllAsync(
            firstPage,
            async (uri, ct) =>
            {
                var page = await GetAsync<PaginatedResponse<JsonElement>>(uri, ct)
                    .ConfigureAwait(false);
                return new PaginatedResult<JsonElement>(
                    page.Values ?? [],
                    GetNextUri(page.Next));
            },
            null,
            cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = _mapper.Map(activity);
            if (shouldStop(model))
            {
                stop = true;
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
        var firstPage = new Uri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests/{pullRequestId.Value}/commits" +
            $"?pagelen={_options.PageLength}&sort=-date",
            UriKind.Relative);

        await foreach (var commit in _paginatorHelper.ReadAllAsync(
            firstPage,
            async (uri, ct) =>
            {
                var page = await GetAsync<PaginatedResponse<PullRequestCommitDto>>(uri, ct)
                    .ConfigureAwait(false);
                return new PaginatedResult<PullRequestCommitDto>(
                    page.Values ?? [],
                    GetNextUri(page.Next));
            },
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

        try
        {
            var diffStatUri = new Uri(
                $"repositories/{workspace.Value}/{repoSlug.Value}/diffstat/{commitHash}" +
                $"?topic=true&pagelen={_options.PageLength}",
                UriKind.Relative);

            return await ReadPullRequestDiffStatAsync(diffStatUri, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return PullRequestSizeSummary.Empty;
        }
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

        try
        {
            var cacheKey = BuildPullRequestCacheKey(workspace, repoSlug, pullRequestId.Value);
            var sourceCommitHash = default(string);
            var destinationCommitHash = default(string);

            if (_pullRequestCommitRanges.TryGetValue(cacheKey, out var cachedCommitRange))
            {
                sourceCommitHash = cachedCommitRange.SourceCommitHash;
                destinationCommitHash = cachedCommitRange.DestinationCommitHash;
            }

            if (string.IsNullOrWhiteSpace(sourceCommitHash) ||
                string.IsNullOrWhiteSpace(destinationCommitHash))
            {
                var pullRequestUri = new Uri(
                    $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests/{pullRequestId.Value}",
                    UriKind.Relative);
                var pullRequestReference = await GetAsync<PullRequestSizeReferenceDto>(pullRequestUri, cancellationToken)
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
                return PullRequestSizeSummary.Empty;
            }

            var diffStatUri = new Uri(
                $"repositories/{workspace.Value}/{repoSlug.Value}/diffstat/" +
                $"{workspace.Value}/{repoSlug.Value}:{sourceCommitHash}..{destinationCommitHash}" +
                $"?topic=true&pagelen={_options.PageLength}",
                UriKind.Relative);

            return await ReadPullRequestDiffStatAsync(diffStatUri, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return PullRequestSizeSummary.Empty;
        }
    }

    private async Task<PullRequestSizeSummary> ReadPullRequestDiffStatAsync(
        Uri firstPageUri,
        CancellationToken cancellationToken)
    {
        var filesChanged = 0;
        var linesAdded = 0;
        var linesRemoved = 0;

        await foreach (var entry in _paginatorHelper.ReadAllAsync(
            firstPageUri,
            async (uri, ct) =>
            {
                var page = await GetAsync<PaginatedResponse<PullRequestDiffStatDto>>(uri, ct)
                    .ConfigureAwait(false);
                return new PaginatedResult<PullRequestDiffStatDto>(
                    page.Values ?? [],
                    GetNextUri(page.Next));
            },
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

    private Task<T> GetAsync<T>(Uri url, CancellationToken cancellationToken) =>
        _transport.GetAsync<T>(url, cancellationToken);

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
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);

        if (string.IsNullOrWhiteSpace(sourceCommitHash) ||
            string.IsNullOrWhiteSpace(destinationCommitHash))
        {
            return;
        }

        var cacheKey = BuildPullRequestCacheKey(workspace, repoSlug, pullRequestId);
        _pullRequestCommitRanges[cacheKey] = new PullRequestCommitRange(sourceCommitHash, destinationCommitHash);
    }

    private Uri BuildPullRequestsUri(Workspace workspace, RepoSlug repoSlug)
    {
        var query = BuildPullRequestQuery();
        var path =
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests?pagelen={_options.PageLength}" +
            $"&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on";

        if (!string.IsNullOrWhiteSpace(query))
        {
            path += $"&q={Uri.EscapeDataString(query)}";
        }

        return new Uri(path, UriKind.Relative);
    }

    private string? BuildPullRequestQuery()
    {
        var clauses = new List<string>(capacity: 2);
        var filterDate = FormatQueryDate(_reportParameters.FilterDate);

        switch (_reportParameters.PrTimeFilterMode)
        {
            case PrTimeFilterMode.CreatedOnOnly:
                clauses.Add($"created_on >= {filterDate}");
                break;
            case PrTimeFilterMode.LastKnownUpdateAndCreated:
                clauses.Add($"(created_on >= {filterDate} OR updated_on >= {filterDate})");
                break;
            default:
                throw new NotImplementedException();
        }

        if (_reportParameters.ToDateExclusive.HasValue)
        {
            clauses.Add($"created_on < {FormatQueryDate(_reportParameters.ToDateExclusive.Value)}");
        }

        return clauses.Count == 0
            ? null
            : string.Join(" AND ", clauses);
    }

    private static string FormatQueryDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string BuildPullRequestCacheKey(Workspace workspace, RepoSlug repoSlug, int pullRequestId) =>
        $"{workspace.Value}/{repoSlug.Value}/{pullRequestId.ToString(CultureInfo.InvariantCulture)}";

    private static Uri? GetNextUri(string? next) => string.IsNullOrWhiteSpace(next) ? null : new Uri(next, UriKind.RelativeOrAbsolute);

    private readonly BitbucketOptions _options;
    private readonly ReportParameters _reportParameters;
    private readonly IBitbucketTransport _transport;
    private readonly IPaginatorHelper _paginatorHelper;
    private readonly IBitbucketMapper _mapper;
    private readonly ConcurrentDictionary<string, PullRequestCommitRange> _pullRequestCommitRanges = new(StringComparer.Ordinal);

    private readonly record struct PullRequestCommitRange(string SourceCommitHash, string DestinationCommitHash);
}

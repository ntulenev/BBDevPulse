using System.Runtime.CompilerServices;

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
        _options = options.Value;
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
        var firstPage = new Uri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests?pagelen={_options.PageLength}" +
            $"&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on",
            UriKind.Relative);
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

    private Task<T> GetAsync<T>(Uri url, CancellationToken cancellationToken) =>
        _transport.GetAsync<T>(url, cancellationToken);

    private static Uri? GetNextUri(string? next) => string.IsNullOrWhiteSpace(next) ? null : new Uri(next, UriKind.RelativeOrAbsolute);

    private readonly BitbucketOptions _options;
    private readonly IBitbucketTransport _transport;
    private readonly IPaginatorHelper _paginatorHelper;
    private readonly IBitbucketMapper _mapper;
}

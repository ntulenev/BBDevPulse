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
public sealed class BitbucketClient : IBitbucketClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketClient"/> class.
    /// </summary>
    /// <param name="httpClient">Configured HTTP client.</param>
    /// <param name="options">Bitbucket options.</param>
    /// <param name="activityMapper">Activity mapper.</param>
    public BitbucketClient(
        HttpClient httpClient,
        IOptions<BitbucketOptions> options,
        IPullRequestActivityMapper activityMapper)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(activityMapper);
        _httpClient = httpClient;
        _options = options.Value;
        _activityMapper = activityMapper;
    }

    /// <inheritdoc />
    public async Task<AuthUser> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var dto = await GetAsync<AuthUserDto>(new Uri("user", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);
        return Map(dto);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Repository> GetRepositoriesAsync(
        Workspace workspace,
        Action<int>? onPage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var next = new Uri($"repositories/{workspace.Value}?pagelen={_options.PageLength}", UriKind.Relative);
        var pageIndex = 0;

        while (next is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageIndex++;
            onPage?.Invoke(pageIndex);

            var page = await GetAsync<PaginatedResponse<RepositoryDto>>(next, cancellationToken)
                .ConfigureAwait(false);
            if (page.Values is { Count: > 0 })
            {
                foreach (var repo in page.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return Map(repo);
                }
            }

            next = GetNextUri(page.Next);
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
        var next = new Uri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests?pagelen={_options.PageLength}&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on",
            UriKind.Relative);
        var stop = false;

        while (next is not null && !stop)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await GetAsync<PaginatedResponse<PullRequestDto>>(next, cancellationToken)
                .ConfigureAwait(false);
            var pullRequests = page.Values ?? [];

            foreach (var pr in pullRequests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var model = Map(pr);
                if (shouldStop(model))
                {
                    stop = true;
                    break;
                }

                yield return model;
            }

            if (!stop)
            {
                next = GetNextUri(page.Next);
            }
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
        var next = new Uri(
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests/{pullRequestId.Value}/activity?pagelen={_options.PageLength}&sort=-created_on",
            UriKind.Relative);
        var stop = false;

        while (next is not null && !stop)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await GetAsync<PaginatedResponse<JsonElement>>(next, cancellationToken)
                .ConfigureAwait(false);
            var activities = page.Values ?? [];

            foreach (var activity in activities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var model = _activityMapper.Map(activity);
                if (shouldStop(model))
                {
                    stop = true;
                    break;
                }

                yield return model;
            }

            if (!stop)
            {
                next = GetNextUri(page.Next);
            }
        }

    }

    private async Task<T> GetAsync<T>(Uri url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Bitbucket API request failed ({response.StatusCode}): {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result == null)
        {
            throw new InvalidOperationException("Bitbucket API response was empty.");
        }

        return result;
    }

    private static Uri? GetNextUri(string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            return null;
        }

        return new Uri(next, UriKind.RelativeOrAbsolute);
    }

    private static AuthUser Map(AuthUserDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new AuthUser(
            new DisplayName(dto.DisplayName ?? string.Empty),
            new Username(dto.Username ?? string.Empty),
            new UserUuid(dto.Uuid ?? string.Empty));
    }

    private static Repository Map(RepositoryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new Repository(
            new RepoName(dto.Name ?? string.Empty),
            new RepoSlug(dto.Slug ?? string.Empty));
    }

    private static PullRequest Map(PullRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new PullRequest(
            new PullRequestId(dto.Id),
            MapState(dto.State),
            dto.ClosedOn,
            dto.CreatedOn,
            dto.UpdatedOn,
            dto.MergedOn,
            Map(dto.Author),
            Map(dto.Destination));
    }

    private static User? Map(UserDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        return new User(
            new DisplayName(dto.DisplayName ?? string.Empty),
            new UserUuid(dto.Uuid ?? string.Empty));
    }

    private static PullRequestDestination? Map(PullRequestDestinationDto? dto)
    {
        if (dto?.Branch is null)
        {
            return null;
        }

        return new PullRequestDestination(Map(dto.Branch));
    }

    private static PullRequestBranch Map(PullRequestBranchDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new PullRequestBranch(dto.Name ?? string.Empty);
    }

    private static PullRequestState MapState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return PullRequestState.Unknown;
        }

        return state.Trim().ToUpperInvariant() switch
        {
            "OPEN" => PullRequestState.Open,
            "MERGED" => PullRequestState.Merged,
            "DECLINED" => PullRequestState.Declined,
            "SUPERSEDED" => PullRequestState.Superseded,
            _ => PullRequestState.Unknown
        };
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly BitbucketOptions _options;
    private readonly IPullRequestActivityMapper _activityMapper;
}

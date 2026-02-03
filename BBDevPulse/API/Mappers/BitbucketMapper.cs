using BBDevPulse.Abstractions;
using BBDevPulse.Models;
using BBDevPulse.Transport;

using System.Text.Json;

namespace BBDevPulse.API.Mappers;

/// <summary>
/// Maps Bitbucket DTOs into domain models.
/// </summary>
internal sealed class BitbucketMapper : IBitbucketMapper
{
    private readonly IPullRequestActivityMapper _activityMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketMapper"/> class.
    /// </summary>
    /// <param name="activityMapper">Activity mapper.</param>
    public BitbucketMapper(IPullRequestActivityMapper activityMapper)
    {
        ArgumentNullException.ThrowIfNull(activityMapper);
        _activityMapper = activityMapper;
    }

    /// <inheritdoc />
    public AuthUser Map(AuthUserDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new AuthUser(
            new DisplayName(dto.DisplayName ?? string.Empty),
            new Username(dto.Username ?? string.Empty),
            new UserUuid(dto.Uuid ?? string.Empty));
    }

    /// <inheritdoc />
    public Repository Map(RepositoryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new Repository(
            new RepoName(dto.Name ?? string.Empty),
            new RepoSlug(dto.Slug ?? string.Empty));
    }

    /// <inheritdoc />
    public PullRequest Map(PullRequestDto dto)
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

    /// <inheritdoc />
    public PullRequestActivity Map(JsonElement activity) => _activityMapper.Map(activity);

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
}

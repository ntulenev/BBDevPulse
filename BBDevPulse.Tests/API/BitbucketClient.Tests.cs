using System.Text.Json;

using FluentAssertions;

using BBDevPulse.API;
using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;
using BBDevPulse.Transport;

using Microsoft.Extensions.Options;

using Moq;

namespace BBDevPulse.Tests.API;

public sealed class BitbucketClientTests
{
    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new BitbucketClient(
            options,
            new Mock<IBitbucketTransport>(MockBehavior.Strict).Object,
            new PaginatorHelper(),
            new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when transport is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTransportIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketTransport transport = null!;

        // Act
        Action act = () => _ = new BitbucketClient(
            Options.Create(CreateOptions()),
            transport,
            new PaginatorHelper(),
            new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when paginator helper is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenPaginatorHelperIsNullThrowsArgumentNullException()
    {
        // Arrange
        IPaginatorHelper paginatorHelper = null!;

        // Act
        Action act = () => _ = new BitbucketClient(
            Options.Create(CreateOptions()),
            new Mock<IBitbucketTransport>(MockBehavior.Strict).Object,
            paginatorHelper,
            new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when mapper is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenMapperIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketMapper mapper = null!;

        // Act
        Action act = () => _ = new BitbucketClient(
            Options.Create(CreateOptions()),
            new Mock<IBitbucketTransport>(MockBehavior.Strict).Object,
            new PaginatorHelper(),
            mapper);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetCurrentUserAsync maps authenticated user DTO")]
    [Trait("Category", "Unit")]
    public async Task GetCurrentUserAsyncWhenTransportReturnsDtoReturnsMappedModel()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);
        var transportCalls = 0;
        var mapCalls = 0;
        var dto = new AuthUserDto
        {
            DisplayName = "Alice",
            Username = "alice",
            Uuid = "{alice-1}"
        };
        var expected = new AuthUser(
            new DisplayName("Alice"),
            new Username("alice"),
            new UserUuid("{alice-1}"));

        transport.Setup(x => x.GetAsync<AuthUserDto>(
                It.Is<Uri>(u => u.ToString() == "user"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => transportCalls++)
            .ReturnsAsync(dto);
        mapper.Setup(x => x.Map(It.Is<AuthUserDto>(value => ReferenceEquals(value, dto))))
            .Callback(() => mapCalls++)
            .Returns(expected);

        var client = CreateClient(transport.Object, mapper.Object);

        // Act
        var result = await client.GetCurrentUserAsync(cancellationToken);

        // Assert
        result.Should().BeSameAs(expected);
        transportCalls.Should().Be(1);
        mapCalls.Should().Be(1);
    }

    [Fact(DisplayName = "GetRepositoriesAsync throws when workspace is null")]
    [Trait("Category", "Unit")]
    public async Task GetRepositoriesAsyncWhenWorkspaceIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        Workspace workspace = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetRepositoriesAsync(workspace, null, cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetRepositoriesAsync reads all pages and maps repositories")]
    [Trait("Category", "Unit")]
    public async Task GetRepositoriesAsyncWhenPagesExistReturnsMappedRepositories()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);
        var transportCalls = 0;
        var mapCalls = 0;
        var pages = new Dictionary<string, PaginatedResponse<RepositoryDto>>(StringComparer.Ordinal)
        {
            ["repositories/ws?pagelen=25"] = new PaginatedResponse<RepositoryDto>
            {
                Values =
                [
                    new RepositoryDto { Name = "Repo1", Slug = "repo-1" }
                ],
                Next = "repositories/ws?page=2"
            },
            ["repositories/ws?page=2"] = new PaginatedResponse<RepositoryDto>
            {
                Values =
                [
                    new RepositoryDto { Name = "Repo2", Slug = "repo-2" }
                ],
                Next = "   "
            }
        };

        transport.Setup(x => x.GetAsync<PaginatedResponse<RepositoryDto>>(
                It.Is<Uri>(uri => pages.ContainsKey(uri.ToString())),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => transportCalls++)
            .ReturnsAsync((Uri uri, CancellationToken _) => pages[uri.ToString()]);

        mapper.Setup(x => x.Map(It.Is<RepositoryDto>(dto =>
                dto.Name != null &&
                (dto.Slug == "repo-1" || dto.Slug == "repo-2"))))
            .Callback(() => mapCalls++)
            .Returns((RepositoryDto dto) => new Repository(new RepoName(dto.Name ?? string.Empty), new RepoSlug(dto.Slug ?? string.Empty)));

        var client = CreateClient(transport.Object, mapper.Object);
        var pageIndexes = new List<int>();

        // Act
        var result = await ReadAllAsync(client.GetRepositoriesAsync(
            new Workspace("ws"),
            page => pageIndexes.Add(page),
            cancellationToken));

        // Assert
        result.Select(repo => repo.Slug.Value).Should().Equal("repo-1", "repo-2");
        pageIndexes.Should().Equal(1, 2);
        transportCalls.Should().Be(2);
        mapCalls.Should().Be(2);
    }

    [Fact(DisplayName = "GetRepositoriesAsync checks cancellation while streaming results")]
    [Trait("Category", "Unit")]
    public async Task GetRepositoriesAsyncWhenCanceledDuringIterationThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);
        transport.Setup(x => x.GetAsync<PaginatedResponse<RepositoryDto>>(
                It.Is<Uri>(uri => uri.ToString() == "repositories/ws?pagelen=25"),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync(new PaginatedResponse<RepositoryDto>
            {
                Values =
                [
                    new RepositoryDto { Name = "Repo1", Slug = "repo-1" },
                    new RepositoryDto { Name = "Repo2", Slug = "repo-2" }
                ],
                Next = null
            });
        mapper.Setup(x => x.Map(It.Is<RepositoryDto>(dto =>
                dto.Name != null &&
                (dto.Slug == "repo-1" || dto.Slug == "repo-2"))))
            .Returns((RepositoryDto dto) => new Repository(new RepoName(dto.Name ?? string.Empty), new RepoSlug(dto.Slug ?? string.Empty)));

        var client = CreateClient(transport.Object, mapper.Object);

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var repo in client.GetRepositoriesAsync(new Workspace("ws"), null, cts.Token))
            {
                if (repo.Slug.Value == "repo-1")
                {
                    cts.Cancel();
                }
            }
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "GetRepositoriesAsync treats null page values as empty")]
    [Trait("Category", "Unit")]
    public async Task GetRepositoriesAsyncWhenPageValuesAreNullReturnsEmptySequence()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);
        var mapCalls = 0;
        transport.Setup(x => x.GetAsync<PaginatedResponse<RepositoryDto>>(
                It.Is<Uri>(uri => uri.ToString() == "repositories/ws?pagelen=25"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<RepositoryDto>
            {
                Values = null!,
                Next = null
            });
        mapper.Setup(x => x.Map(It.Is<RepositoryDto>(dto =>
                !string.IsNullOrWhiteSpace(dto.Name) &&
                !string.IsNullOrWhiteSpace(dto.Slug))))
            .Callback(() => mapCalls++)
            .Returns((RepositoryDto dto) => new Repository(new RepoName(dto.Name ?? string.Empty), new RepoSlug(dto.Slug ?? string.Empty)));

        var client = CreateClient(transport.Object, mapper.Object);

        // Act
        var result = await ReadAllAsync(client.GetRepositoriesAsync(new Workspace("ws"), null, cancellationToken));

        // Assert
        result.Should().BeEmpty();
        mapCalls.Should().Be(0);
    }

    [Fact(DisplayName = "GetPullRequestsAsync throws when workspace is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestsAsyncWhenWorkspaceIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        Workspace workspace = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestsAsync(
                               workspace,
                               new RepoSlug("repo"),
                               _ => false,
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestsAsync throws when repository slug is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestsAsyncWhenRepositorySlugIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        RepoSlug repoSlug = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestsAsync(
                               new Workspace("ws"),
                               repoSlug,
                               _ => false,
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestsAsync throws when stop predicate is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestsAsyncWhenStopPredicateIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        Func<PullRequest, bool> shouldStop = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestsAsync(
                               new Workspace("ws"),
                               new RepoSlug("repo"),
                               shouldStop,
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestsAsync stops iteration when stop predicate returns true")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestsAsyncWhenStopPredicateReturnsTrueBreaksIteration()
    {
        // Arrange
        var options = CreateOptions(fromDate: "2026-02-15", toDate: "2026-03-31");
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);

        transport.Setup(x => x.GetAsync<PaginatedResponse<PullRequestDto>>(
                It.Is<Uri>(uri => uri.ToString() ==
                    CreatePullRequestRequestUri("created_on >= 2026-02-15T00:00:00+00:00 AND created_on < 2026-04-01T00:00:00+00:00")),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<PullRequestDto>
            {
                Values =
                [
                    new PullRequestDto { Id = 1, CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero), State = "OPEN" },
                    new PullRequestDto { Id = 2, CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero), State = "OPEN" }
                ],
                Next = null
            });

        mapper.Setup(x => x.Map(It.Is<PullRequestDto>(dto =>
                (dto.Id == 1 || dto.Id == 2) &&
                string.Equals(dto.State, "OPEN", StringComparison.Ordinal))))
            .Returns((PullRequestDto dto) => new PullRequest(
                new PullRequestId(dto.Id),
                PullRequestState.Open,
                closedOn: null,
                createdOn: dto.CreatedOn,
                updatedOn: null,
                mergedOn: null,
                author: null,
                destination: null));

        var client = CreateClient(transport.Object, mapper.Object, options);

        // Act
        var result = await ReadAllAsync(client.GetPullRequestsAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            pullRequest => pullRequest.Id.Value == 2,
            cancellationToken));

        // Assert
        result.Select(pr => pr.Id.Value).Should().Equal(1);
    }

    [Fact(DisplayName = "GetPullRequestsAsync returns all pull requests when stop predicate never matches")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestsAsyncWhenStopPredicateNeverMatchesReturnsAllItems()
    {
        // Arrange
        var options = CreateOptions(fromDate: "2026-02-15", toDate: "2026-03-31");
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);

        transport.Setup(x => x.GetAsync<PaginatedResponse<PullRequestDto>>(
                It.Is<Uri>(uri => uri.ToString() ==
                    CreatePullRequestRequestUri("created_on >= 2026-02-15T00:00:00+00:00 AND created_on < 2026-04-01T00:00:00+00:00")),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<PullRequestDto>
            {
                Values =
                [
                    new PullRequestDto { Id = 1, CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero), State = "OPEN" },
                    new PullRequestDto { Id = 2, CreatedOn = new DateTimeOffset(2026, 2, 20, 1, 0, 0, TimeSpan.Zero), State = "OPEN" }
                ],
                Next = null
            });

        mapper.Setup(x => x.Map(It.Is<PullRequestDto>(dto =>
                (dto.Id == 1 || dto.Id == 2) &&
                string.Equals(dto.State, "OPEN", StringComparison.Ordinal))))
            .Returns((PullRequestDto dto) => new PullRequest(
                new PullRequestId(dto.Id),
                PullRequestState.Open,
                closedOn: null,
                createdOn: dto.CreatedOn,
                updatedOn: null,
                mergedOn: null,
                author: null,
                destination: null));

        var client = CreateClient(transport.Object, mapper.Object, options);

        // Act
        var result = await ReadAllAsync(client.GetPullRequestsAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            _ => false,
            cancellationToken));

        // Assert
        result.Select(pr => pr.Id.Value).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "GetPullRequestsAsync treats null page values as empty")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestsAsyncWhenPageValuesAreNullReturnsEmptySequence()
    {
        // Arrange
        var options = CreateOptions(fromDate: "2026-02-15", toDate: "2026-03-31");
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);
        var mapCalls = 0;

        transport.Setup(x => x.GetAsync<PaginatedResponse<PullRequestDto>>(
                It.Is<Uri>(uri => uri.ToString() ==
                    CreatePullRequestRequestUri("created_on >= 2026-02-15T00:00:00+00:00 AND created_on < 2026-04-01T00:00:00+00:00")),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<PullRequestDto>
            {
                Values = null!,
                Next = null
            });
        mapper.Setup(x => x.Map(It.Is<PullRequestDto>(dto => dto.Id > 0)))
            .Callback(() => mapCalls++)
            .Returns((PullRequestDto dto) => new PullRequest(
                new PullRequestId(dto.Id),
                PullRequestState.Open,
                closedOn: null,
                createdOn: dto.CreatedOn,
                updatedOn: null,
                mergedOn: null,
                author: null,
                destination: null));

        var client = CreateClient(transport.Object, mapper.Object, options);

        // Act
        var result = await ReadAllAsync(client.GetPullRequestsAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            _ => false,
            cancellationToken));

        // Assert
        result.Should().BeEmpty();
        mapCalls.Should().Be(0);
    }

    [Fact(DisplayName = "GetPullRequestsAsync filters by created or updated date for historical activity mode")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestsAsyncWhenLastKnownUpdateModeConfiguredAddsServerSideDateQuery()
    {
        // Arrange
        var options = CreateOptions(
            prTimeFilterMode: PrTimeFilterMode.LastKnownUpdateAndCreated,
            fromDate: "2026-02-15",
            toDate: "2026-03-31");
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);
        transport.Setup(x => x.GetAsync<PaginatedResponse<PullRequestDto>>(
                It.Is<Uri>(uri => uri.ToString() ==
                    CreatePullRequestRequestUri("(created_on >= 2026-02-15T00:00:00+00:00 OR updated_on >= 2026-02-15T00:00:00+00:00) AND created_on < 2026-04-01T00:00:00+00:00")),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<PullRequestDto>
            {
                Values = [],
                Next = null
            });

        var client = CreateClient(transport.Object, mapper.Object, options);

        // Act
        var result = await ReadAllAsync(client.GetPullRequestsAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            _ => false,
            cancellationToken));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "GetPullRequestActivityAsync throws when workspace is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestActivityAsyncWhenWorkspaceIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        Workspace workspace = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestActivityAsync(
                               workspace,
                               new RepoSlug("repo"),
                               new PullRequestId(1),
                               _ => false,
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestActivityAsync throws when repository slug is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestActivityAsyncWhenRepositorySlugIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        RepoSlug repoSlug = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestActivityAsync(
                               new Workspace("ws"),
                               repoSlug,
                               new PullRequestId(1),
                               _ => false,
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestActivityAsync throws when pull request id is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestActivityAsyncWhenPullRequestIdIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        PullRequestId pullRequestId = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestActivityAsync(
                               new Workspace("ws"),
                               new RepoSlug("repo"),
                               pullRequestId,
                               _ => false,
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestActivityAsync throws when stop predicate is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestActivityAsyncWhenStopPredicateIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        Func<PullRequestActivity, bool> shouldStop = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestActivityAsync(
                               new Workspace("ws"),
                               new RepoSlug("repo"),
                               new PullRequestId(1),
                               shouldStop,
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestActivityAsync stops iteration when stop predicate returns true")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestActivityAsyncWhenStopPredicateReturnsTrueBreaksIteration()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);
        var firstActivity = ParseJson(/*lang=json,strict*/ """{"id":1}""");
        var secondActivity = ParseJson(/*lang=json,strict*/ """{"id":2}""");
        var firstModel = new PullRequestActivity(
            new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero),
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);
        var secondModel = new PullRequestActivity(
            new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero),
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        transport.Setup(x => x.GetAsync<PaginatedResponse<JsonElement>>(
                It.Is<Uri>(uri => uri.ToString() == "repositories/ws/repo/pullrequests/10/activity?pagelen=25&sort=-created_on"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<JsonElement>
            {
                Values = [firstActivity, secondActivity],
                Next = null
            });

        mapper.Setup(x => x.Map(It.Is<JsonElement>(e => e.GetProperty("id").GetInt32() == 1)))
            .Returns(firstModel);
        mapper.Setup(x => x.Map(It.Is<JsonElement>(e => e.GetProperty("id").GetInt32() == 2)))
            .Returns(secondModel);

        var client = CreateClient(transport.Object, mapper.Object);

        // Act
        var result = await ReadAllAsync(client.GetPullRequestActivityAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            new PullRequestId(10),
            activity => activity.ActivityDate == secondModel.ActivityDate,
            cancellationToken));

        // Assert
        result.Should().ContainSingle();
        result[0].ActivityDate.Should().Be(firstModel.ActivityDate);
    }

    [Fact(DisplayName = "GetPullRequestActivityAsync treats null page values as empty")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestActivityAsyncWhenPageValuesAreNullReturnsEmptySequence()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        var mapper = new Mock<IBitbucketMapper>(MockBehavior.Strict);
        var mapCalls = 0;
        transport.Setup(x => x.GetAsync<PaginatedResponse<JsonElement>>(
                It.Is<Uri>(uri => uri.ToString() == "repositories/ws/repo/pullrequests/1/activity?pagelen=25&sort=-created_on"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<JsonElement>
            {
                Values = null!,
                Next = null
            });
        mapper.Setup(x => x.Map(It.Is<JsonElement>(element => element.ValueKind == JsonValueKind.Object)))
            .Callback(() => mapCalls++)
            .Returns(new PullRequestActivity(
                activityDate: null,
                mergeDate: null,
                actor: null,
                comment: null,
                approval: null));

        var client = CreateClient(transport.Object, mapper.Object);

        // Act
        var result = await ReadAllAsync(client.GetPullRequestActivityAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            new PullRequestId(1),
            _ => false,
            cancellationToken));

        // Assert
        result.Should().BeEmpty();
        mapCalls.Should().Be(0);
    }

    [Fact(DisplayName = "GetPullRequestCommitsAsync throws when workspace is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestCommitsAsyncWhenWorkspaceIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        Workspace workspace = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestCommitsAsync(
                               workspace,
                               new RepoSlug("repo"),
                               new PullRequestId(1),
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestCommitsAsync throws when repository slug is null")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestCommitsAsyncWhenRepositorySlugIsNullThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient(new Mock<IBitbucketTransport>(MockBehavior.Strict).Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);
        RepoSlug repoSlug = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in client.GetPullRequestCommitsAsync(
                               new Workspace("ws"),
                               repoSlug,
                               new PullRequestId(1),
                               cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPullRequestCommitsAsync yields only commits that have hash date and message values")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestCommitsAsyncWhenCommitDetailsExistReturnsOnlyCompleteCommits()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        transport.Setup(x => x.GetAsync<PaginatedResponse<PullRequestCommitDto>>(
                It.Is<Uri>(uri => uri.ToString() == "repositories/ws/repo/pullrequests/7/commits?pagelen=25&sort=-date"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<PullRequestCommitDto>
            {
                Values =
                [
                    new PullRequestCommitDto
                    {
                        Hash = "abcdef123456",
                        Date = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero),
                        Summary = new PullRequestCommitDto.CommitSummaryDto { Raw = "Add follow-up metrics" }
                    },
                    new PullRequestCommitDto
                    {
                        Hash = "missing-date",
                        Date = null,
                        Summary = new PullRequestCommitDto.CommitSummaryDto { Raw = "Missing date" }
                    },
                    new PullRequestCommitDto
                    {
                        Hash = null,
                        Date = new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero),
                        Summary = new PullRequestCommitDto.CommitSummaryDto { Raw = "Missing hash" }
                    },
                    new PullRequestCommitDto
                    {
                        Hash = "missing-message",
                        Date = new DateTimeOffset(2026, 2, 20, 8, 0, 0, TimeSpan.Zero),
                        Summary = null
                    },
                    new PullRequestCommitDto
                    {
                        Hash = "fedcba654321",
                        Date = new DateTimeOffset(2026, 2, 19, 9, 0, 0, TimeSpan.Zero),
                        Summary = new PullRequestCommitDto.CommitSummaryDto { Raw = "Fix router retry flow" }
                    }
                ],
                Next = null
            });

        var client = CreateClient(transport.Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);

        // Act
        var result = await ReadAllAsync(client.GetPullRequestCommitsAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            new PullRequestId(7),
            cancellationToken));

        // Assert
        result.Should().BeEquivalentTo(
            [
                new PullRequestCommitInfo("abcdef123456", new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero), "Add follow-up metrics"),
                new PullRequestCommitInfo("fedcba654321", new DateTimeOffset(2026, 2, 19, 9, 0, 0, TimeSpan.Zero), "Fix router retry flow")
            ],
            options => options.ComparingByMembers<PullRequestCommitInfo>());
    }

    [Fact(DisplayName = "GetCommitSizeAsync aggregates files and line counts from commit diffstat")]
    [Trait("Category", "Unit")]
    public async Task GetCommitSizeAsyncWhenCommitHashExistsReturnsAggregatedSummary()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        transport.Setup(x => x.GetAsync<PaginatedResponse<PullRequestDiffStatDto>>(
                It.Is<Uri>(uri => uri.ToString() ==
                    "repositories/ws/repo/diffstat/commit-hash-123?topic=true&pagelen=25"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<PullRequestDiffStatDto>
            {
                Values =
                [
                    new PullRequestDiffStatDto { LinesAdded = 12, LinesRemoved = 4 },
                    new PullRequestDiffStatDto { LinesAdded = 3, LinesRemoved = 1 }
                ],
                Next = null
            });

        var client = CreateClient(transport.Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);

        // Act
        var result = await client.GetCommitSizeAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            "commit-hash-123",
            cancellationToken);

        // Assert
        result.FilesChanged.Should().Be(2);
        result.LinesAdded.Should().Be(15);
        result.LinesRemoved.Should().Be(5);
        result.LineChurn.Should().Be(20);
    }

    [Fact(DisplayName = "GetPullRequestSizeAsync aggregates files and line counts from commit range diffstat")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestSizeAsyncWhenCommitHashesExistReturnsAggregatedSummary()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        transport.Setup(x => x.GetAsync<PullRequestSizeReferenceDto>(
                It.Is<Uri>(uri => uri.ToString() == "repositories/ws/repo/pullrequests/7"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PullRequestSizeReferenceDto
            {
                Source = new PullRequestEndpointDto
                {
                    Commit = new PullRequestCommitHashDto { Hash = "source-hash" }
                },
                Destination = new PullRequestEndpointDto
                {
                    Commit = new PullRequestCommitHashDto { Hash = "destination-hash" }
                }
            });
        transport.Setup(x => x.GetAsync<PaginatedResponse<PullRequestDiffStatDto>>(
                It.Is<Uri>(uri => uri.ToString() ==
                    "repositories/ws/repo/diffstat/ws/repo:source-hash..destination-hash?topic=true&pagelen=25"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PaginatedResponse<PullRequestDiffStatDto>
            {
                Values =
                [
                    new PullRequestDiffStatDto { LinesAdded = 10, LinesRemoved = 3 },
                    new PullRequestDiffStatDto { LinesAdded = 5, LinesRemoved = 2 }
                ],
                Next = null
            });

        var client = CreateClient(transport.Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);

        // Act
        var result = await client.GetPullRequestSizeAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            new PullRequestId(7),
            cancellationToken);

        // Assert
        result.FilesChanged.Should().Be(2);
        result.LinesAdded.Should().Be(15);
        result.LinesRemoved.Should().Be(5);
        result.LineChurn.Should().Be(20);
    }

    [Fact(DisplayName = "GetPullRequestSizeAsync returns empty size when diffstat request fails")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestSizeAsyncWhenDiffStatFailsReturnsEmpty()
    {
        // Arrange
        var transport = new Mock<IBitbucketTransport>(MockBehavior.Strict);
        transport.Setup(x => x.GetAsync<PullRequestSizeReferenceDto>(
                It.Is<Uri>(uri => uri.ToString() == "repositories/ws/repo/pullrequests/7"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PullRequestSizeReferenceDto
            {
                Source = new PullRequestEndpointDto
                {
                    Commit = new PullRequestCommitHashDto { Hash = "source-hash" }
                },
                Destination = new PullRequestEndpointDto
                {
                    Commit = new PullRequestCommitHashDto { Hash = "destination-hash" }
                }
            });
        transport.Setup(x => x.GetAsync<PaginatedResponse<PullRequestDiffStatDto>>(
                It.Is<Uri>(uri => uri.ToString() ==
                    "repositories/ws/repo/diffstat/ws/repo:source-hash..destination-hash?topic=true&pagelen=25"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ThrowsAsync(new InvalidOperationException("Bitbucket API request failed (Forbidden): auth"));

        var client = CreateClient(transport.Object, new Mock<IBitbucketMapper>(MockBehavior.Strict).Object);

        // Act
        var result = await client.GetPullRequestSizeAsync(
            new Workspace("ws"),
            new RepoSlug("repo"),
            new PullRequestId(7),
            cancellationToken);

        // Assert
        result.Should().Be(PullRequestSizeSummary.Empty);
    }

    private static BitbucketClient CreateClient(
        IBitbucketTransport transport,
        IBitbucketMapper mapper,
        BitbucketOptions? options = null)
    {
        return new BitbucketClient(
            Options.Create(options ?? CreateOptions()),
            transport,
            new PaginatorHelper(),
            mapper);
    }

    private static BitbucketOptions CreateOptions(
        PrTimeFilterMode prTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
        string? fromDate = null,
        string? toDate = null)
    {
        return new BitbucketOptions
        {
            Days = string.IsNullOrWhiteSpace(fromDate) && string.IsNullOrWhiteSpace(toDate)
                ? 7
                : null,
            Workspace = "ws",
            FromDate = fromDate,
            ToDate = toDate,
            PageLength = 25,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = prTimeFilterMode,
            Pdf = new PdfOptions()
        };
    }

    private static string CreatePullRequestRequestUri(string query) =>
        "repositories/ws/repo/pullrequests?pagelen=25&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on" +
        $"&q={Uri.EscapeDataString(query)}";

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<List<T>> ReadAllAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }
}


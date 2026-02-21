using System.Text.Json;

using FluentAssertions;

using BBDevPulse.API.Mappers;
using BBDevPulse.Abstractions;
using BBDevPulse.Models;
using BBDevPulse.Transport;

using Moq;

namespace BBDevPulse.Tests.API;

public sealed class BitbucketMapperTests
{
    [Fact(DisplayName = "Constructor throws when activity mapper is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenActivityMapperIsNullThrowsArgumentNullException()
    {
        // Arrange
        IPullRequestActivityMapper activityMapper = null!;

        // Act
        Action act = () => _ = new BitbucketMapper(activityMapper);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Map auth user throws when DTO is null")]
    [Trait("Category", "Unit")]
    public void MapAuthUserWhenDtoIsNullThrowsArgumentNullException()
    {
        // Arrange
        var mapper = CreateMapper();
        AuthUserDto dto = null!;

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Map auth user throws when required fields are missing")]
    [Trait("Category", "Unit")]
    public void MapAuthUserWhenFieldsAreMissingThrowsArgumentException()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new AuthUserDto();

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Map auth user maps valid DTO values")]
    [Trait("Category", "Unit")]
    public void MapAuthUserWhenDtoIsValidMapsAllFields()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new AuthUserDto
        {
            DisplayName = "Alice",
            Username = "alice",
            Uuid = "{alice-1}"
        };

        // Act
        var result = mapper.Map(dto);

        // Assert
        result.DisplayName.Value.Should().Be("Alice");
        result.Username.Value.Should().Be("alice");
        result.Uuid.Value.Should().Be("{alice-1}");
    }

    [Fact(DisplayName = "Map auth user throws when username is missing but display name exists")]
    [Trait("Category", "Unit")]
    public void MapAuthUserWhenUsernameIsMissingThrowsArgumentException()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new AuthUserDto
        {
            DisplayName = "Alice",
            Username = null,
            Uuid = "{alice-1}"
        };

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Map auth user throws when UUID is missing but other fields exist")]
    [Trait("Category", "Unit")]
    public void MapAuthUserWhenUuidIsMissingThrowsArgumentException()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new AuthUserDto
        {
            DisplayName = "Alice",
            Username = "alice",
            Uuid = null
        };

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Map repository throws when DTO is null")]
    [Trait("Category", "Unit")]
    public void MapRepositoryWhenDtoIsNullThrowsArgumentNullException()
    {
        // Arrange
        var mapper = CreateMapper();
        RepositoryDto dto = null!;

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Map repository throws when required fields are missing")]
    [Trait("Category", "Unit")]
    public void MapRepositoryWhenFieldsAreMissingThrowsArgumentException()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new RepositoryDto();

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Map repository maps valid DTO values")]
    [Trait("Category", "Unit")]
    public void MapRepositoryWhenDtoIsValidMapsAllFields()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new RepositoryDto
        {
            Name = "RepoA",
            Slug = "repo-a"
        };

        // Act
        var result = mapper.Map(dto);

        // Assert
        result.Name.Value.Should().Be("RepoA");
        result.Slug.Value.Should().Be("repo-a");
    }

    [Fact(DisplayName = "Map repository throws when slug is missing and name exists")]
    [Trait("Category", "Unit")]
    public void MapRepositoryWhenSlugIsMissingThrowsArgumentException()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new RepositoryDto
        {
            Name = "RepoA",
            Slug = null
        };

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Map pull request throws when DTO is null")]
    [Trait("Category", "Unit")]
    public void MapPullRequestWhenDtoIsNullThrowsArgumentNullException()
    {
        // Arrange
        var mapper = CreateMapper();
        PullRequestDto dto = null!;

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Map pull request maps known state and nested author/destination")]
    [Trait("Category", "Unit")]
    public void MapPullRequestWhenPayloadIsValidMapsAllNestedValues()
    {
        // Arrange
        var mapper = CreateMapper();
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var dto = new PullRequestDto
        {
            Id = 11,
            State = " merged ",
            ClosedOn = createdOn.AddHours(5),
            CreatedOn = createdOn,
            UpdatedOn = createdOn.AddHours(2),
            MergedOn = createdOn.AddHours(4),
            Author = new UserDto
            {
                DisplayName = "Alice",
                Uuid = "{alice-1}"
            },
            Destination = new PullRequestDestinationDto
            {
                Branch = new PullRequestBranchDto { Name = "develop" }
            }
        };

        // Act
        var result = mapper.Map(dto);

        // Assert
        result.Id.Value.Should().Be(11);
        result.State.Should().Be(PullRequestState.Merged);
        result.Author.Should().NotBeNull();
        result.Author!.DisplayName.Value.Should().Be("Alice");
        result.Author.Uuid.Value.Should().Be("{alice-1}");
        result.Destination.Should().NotBeNull();
        result.Destination!.Branch!.Name.Should().Be("develop");
    }

    [Theory(DisplayName = "Map pull request maps known states")]
    [InlineData("OPEN", PullRequestState.Open)]
    [InlineData("MERGED", PullRequestState.Merged)]
    [InlineData("DECLINED", PullRequestState.Declined)]
    [InlineData("SUPERSEDED", PullRequestState.Superseded)]
    [Trait("Category", "Unit")]
    public void MapPullRequestWhenStateIsKnownMapsToExpectedEnum(string state, PullRequestState expected)
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new PullRequestDto
        {
            Id = 7,
            State = state,
            CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)
        };

        // Act
        var result = mapper.Map(dto);

        // Assert
        result.State.Should().Be(expected);
    }

    [Theory(DisplayName = "Map pull request maps unknown states to Unknown")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("IN_REVIEW")]
    [Trait("Category", "Unit")]
    public void MapPullRequestWhenStateIsUnknownMapsToUnknown(string? state)
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new PullRequestDto
        {
            Id = 1,
            State = state,
            CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)
        };

        // Act
        var result = mapper.Map(dto);

        // Assert
        result.State.Should().Be(PullRequestState.Unknown);
    }

    [Fact(DisplayName = "Map pull request maps null nested author and destination")]
    [Trait("Category", "Unit")]
    public void MapPullRequestWhenNestedObjectsAreMissingMapsNulls()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new PullRequestDto
        {
            Id = 2,
            State = "OPEN",
            CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            Author = null,
            Destination = new PullRequestDestinationDto { Branch = null }
        };

        // Act
        var result = mapper.Map(dto);

        // Assert
        result.Author.Should().BeNull();
        result.Destination.Should().BeNull();
        result.State.Should().Be(PullRequestState.Open);
    }

    [Fact(DisplayName = "Map pull request throws when author display name is missing")]
    [Trait("Category", "Unit")]
    public void MapPullRequestWhenAuthorDisplayNameIsMissingThrowsArgumentException()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new PullRequestDto
        {
            Id = 5,
            State = "OPEN",
            CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            Author = new UserDto
            {
                DisplayName = null,
                Uuid = "{author-1}"
            }
        };

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Map pull request throws when author UUID is missing")]
    [Trait("Category", "Unit")]
    public void MapPullRequestWhenAuthorUuidIsMissingThrowsArgumentException()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new PullRequestDto
        {
            Id = 6,
            State = "OPEN",
            CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            Author = new UserDto
            {
                DisplayName = "Author",
                Uuid = null
            }
        };

        // Act
        Action act = () => _ = mapper.Map(dto);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Map pull request maps destination branch with missing name to empty string")]
    [Trait("Category", "Unit")]
    public void MapPullRequestWhenDestinationBranchNameIsMissingUsesEmptyBranchName()
    {
        // Arrange
        var mapper = CreateMapper();
        var dto = new PullRequestDto
        {
            Id = 8,
            State = "OPEN",
            CreatedOn = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            Destination = new PullRequestDestinationDto
            {
                Branch = new PullRequestBranchDto
                {
                    Name = null
                }
            }
        };

        // Act
        var result = mapper.Map(dto);

        // Assert
        result.Destination.Should().NotBeNull();
        result.Destination!.Branch!.Name.Should().BeEmpty();
    }

    [Fact(DisplayName = "Map activity delegates to activity mapper")]
    [Trait("Category", "Unit")]
    public void MapActivityWhenPayloadIsProvidedDelegatesToActivityMapper()
    {
        // Arrange
        var expected = new PullRequestActivity(
            activityDate: null,
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);
        var mock = new Mock<IPullRequestActivityMapper>(MockBehavior.Strict);
        var mapCalls = 0;
        var payload = ParseJson("""{"date":"2026-02-20T10:00:00Z"}""");
        mock.Setup(x => x.Map(It.Is<JsonElement>(e => e.ValueKind == JsonValueKind.Object)))
            .Callback(() => mapCalls++)
            .Returns(expected);

        var mapper = new BitbucketMapper(mock.Object);

        // Act
        var result = mapper.Map(payload);

        // Assert
        result.Should().BeSameAs(expected);
        mapCalls.Should().Be(1);
    }

    private static BitbucketMapper CreateMapper()
    {
        var activityMapper = new Mock<IPullRequestActivityMapper>(MockBehavior.Strict);
        return new BitbucketMapper(activityMapper.Object);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

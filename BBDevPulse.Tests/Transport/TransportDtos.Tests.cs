using System.Text.Json;

using FluentAssertions;

using BBDevPulse.Transport;

namespace BBDevPulse.Tests.Transport;

public sealed class TransportDtosTests
{
    [Fact(DisplayName = "AuthUserDto deserializes JSON property names")]
    [Trait("Category", "Unit")]
    public void AuthUserDtoWhenDeserializedMapsAllProperties()
    {
        // Arrange
        var json = """
            {
              "display_name": "Alice",
              "username": "alice",
              "uuid": "{alice-1}"
            }
            """;

        // Act
        var dto = JsonSerializer.Deserialize<AuthUserDto>(json)!;

        // Assert
        dto.DisplayName.Should().Be("Alice");
        dto.Username.Should().Be("alice");
        dto.Uuid.Should().Be("{alice-1}");
    }

    [Fact(DisplayName = "RepositoryDto deserializes name and slug")]
    [Trait("Category", "Unit")]
    public void RepositoryDtoWhenDeserializedMapsAllProperties()
    {
        // Arrange
        var json = """
            {
              "name": "RepoA",
              "slug": "repo-a"
            }
            """;

        // Act
        var dto = JsonSerializer.Deserialize<RepositoryDto>(json)!;

        // Assert
        dto.Name.Should().Be("RepoA");
        dto.Slug.Should().Be("repo-a");
    }

    [Fact(DisplayName = "UserDto deserializes display name and UUID")]
    [Trait("Category", "Unit")]
    public void UserDtoWhenDeserializedMapsAllProperties()
    {
        // Arrange
        var json = """
            {
              "display_name": "Reviewer",
              "uuid": "{reviewer-1}"
            }
            """;

        // Act
        var dto = JsonSerializer.Deserialize<UserDto>(json)!;

        // Assert
        dto.DisplayName.Should().Be("Reviewer");
        dto.Uuid.Should().Be("{reviewer-1}");
    }

    [Fact(DisplayName = "PullRequestBranchDto deserializes branch name")]
    [Trait("Category", "Unit")]
    public void PullRequestBranchDtoWhenDeserializedMapsName()
    {
        // Arrange
        var json = """{"name":"develop"}""";

        // Act
        var dto = JsonSerializer.Deserialize<PullRequestBranchDto>(json)!;

        // Assert
        dto.Name.Should().Be("develop");
    }

    [Fact(DisplayName = "PullRequestDestinationDto deserializes nested branch")]
    [Trait("Category", "Unit")]
    public void PullRequestDestinationDtoWhenDeserializedMapsBranch()
    {
        // Arrange
        var json = """{"branch":{"name":"main"}}""";

        // Act
        var dto = JsonSerializer.Deserialize<PullRequestDestinationDto>(json)!;

        // Assert
        dto.Branch.Should().NotBeNull();
        dto.Branch!.Name.Should().Be("main");
    }

    [Fact(DisplayName = "PullRequestDto deserializes all supported fields")]
    [Trait("Category", "Unit")]
    public void PullRequestDtoWhenDeserializedMapsAllProperties()
    {
        // Arrange
        var json = """
            {
              "id": 77,
              "state": "OPEN",
              "closed_on": "2026-02-20T13:00:00Z",
              "created_on": "2026-02-20T10:00:00Z",
              "updated_on": "2026-02-20T11:00:00Z",
              "merged_on": "2026-02-20T12:00:00Z",
              "author": {
                "display_name": "Alice",
                "uuid": "{alice-1}"
              },
              "destination": {
                "branch": {
                  "name": "develop"
                }
              }
            }
            """;

        // Act
        var dto = JsonSerializer.Deserialize<PullRequestDto>(json)!;

        // Assert
        dto.Id.Should().Be(77);
        dto.State.Should().Be("OPEN");
        dto.ClosedOn.Should().Be(new DateTimeOffset(2026, 2, 20, 13, 0, 0, TimeSpan.Zero));
        dto.CreatedOn.Should().Be(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero));
        dto.UpdatedOn.Should().Be(new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero));
        dto.MergedOn.Should().Be(new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero));
        dto.Author.Should().NotBeNull();
        dto.Author!.DisplayName.Should().Be("Alice");
        dto.Author!.Uuid.Should().Be("{alice-1}");
        dto.Destination.Should().NotBeNull();
        dto.Destination!.Branch.Should().NotBeNull();
        dto.Destination!.Branch!.Name.Should().Be("develop");
    }

    [Fact(DisplayName = "PullRequestCommitDto deserializes commit date")]
    [Trait("Category", "Unit")]
    public void PullRequestCommitDtoWhenDeserializedMapsDate()
    {
        // Arrange
        var json = """{"date":"2026-02-20T10:00:00Z"}""";

        // Act
        var dto = JsonSerializer.Deserialize<PullRequestCommitDto>(json)!;

        // Assert
        dto.Date.Should().Be(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "PaginatedResponse defaults values to empty list and supports next link")]
    [Trait("Category", "Unit")]
    public void PaginatedResponseWhenConstructedHasExpectedDefaultsAndSetters()
    {
        // Arrange
        var response = new PaginatedResponse<int>
        {
            Next = "https://example.test/page/2"
        };

        // Act
        var values = response.Values;
        var next = response.Next;

        // Assert
        values.Should().NotBeNull();
        values.Should().BeEmpty();
        next.Should().Be("https://example.test/page/2");
    }
}

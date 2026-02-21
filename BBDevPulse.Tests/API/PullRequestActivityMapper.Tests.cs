using FluentAssertions;

using System.Text.Json;

using BBDevPulse.API.Mappers;

namespace BBDevPulse.Tests.API;

public sealed class PullRequestActivityMapperTests
{
    [Fact(DisplayName = "Map throws when activity payload is undefined")]
    [Trait("Category", "Unit")]
    public void MapWhenPayloadIsUndefinedThrowsArgumentException()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        JsonElement activity = default;

        // Act
        Action act = () => _ = mapper.Map(activity);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Map resolves comment activity date, actor, and comment details")]
    [Trait("Category", "Unit")]
    public void MapWhenCommentPayloadIsProvidedMapsCommentActivity()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "comment": {
                "created_on": "2026-02-20T10:00:00Z",
                "user": {
                  "display_name": "Jane",
                  "uuid": "{jane-1}"
                }
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero));
        result.MergeDate.Should().BeNull();
        result.Actor.Should().NotBeNull();
        result.Actor!.Value.DisplayName.Value.Should().Be("Jane");
        result.Actor!.Value.Uuid!.Value.Should().Be("{jane-1}");
        result.Comment.Should().NotBeNull();
        result.Comment!.User.DisplayName.Value.Should().Be("Jane");
        result.Comment.Date.Should().Be(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero));
        result.Approval.Should().BeNull();
    }

    [Fact(DisplayName = "Map resolves merge date from merged pull request payload")]
    [Trait("Category", "Unit")]
    public void MapWhenMergedPullRequestPayloadIsProvidedMapsMergeDate()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "pullrequest": {
                "state": "MERGED",
                "merged_on": "2026-02-20T11:00:00Z",
                "updated_on": "2026-02-20T12:00:00Z",
                "author": {
                  "display_name": "Merger",
                  "uuid": "{merger-1}"
                }
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero));
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero));
        result.Actor.Should().NotBeNull();
        result.Actor.Value.DisplayName.Value.Should().Be("Merger");
        result.Comment.Should().BeNull();
        result.Approval.Should().BeNull();
    }

    [Fact(DisplayName = "Map resolves approval payload and falls back display name to UUID when name is missing")]
    [Trait("Category", "Unit")]
    public void MapWhenApprovalUserHasNoDisplayNameFallsBackToUuidForIdentity()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "approval": {
                "approved_on": "2026-02-20T09:30:00Z",
                "user": {
                  "uuid": "{approver-1}"
                }
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 20, 9, 30, 0, TimeSpan.Zero));
        result.Actor.Should().NotBeNull();
        result.Actor!.Value.DisplayName.Value.Should().Be("{approver-1}");
        result.Actor!.Value.Uuid!.Value.Should().Be("{approver-1}");
        result.Approval.Should().NotBeNull();
        result.Approval!.Date.Should().Be(new DateTimeOffset(2026, 2, 20, 9, 30, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Map returns empty activity details when payload does not contain known fields")]
    [Trait("Category", "Unit")]
    public void MapWhenNoKnownFieldsArePresentReturnsActivityWithNullOptionalFields()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """{"unknown": "value"}""");

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().BeNull();
        result.MergeDate.Should().BeNull();
        result.Actor.Should().BeNull();
        result.Comment.Should().BeNull();
        result.Approval.Should().BeNull();
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

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

    [Fact(DisplayName = "Map resolves merged update payload using update date and update author nickname")]
    [Trait("Category", "Unit")]
    public void MapWhenUpdatePayloadMarksMergedUsesUpdateDateAndNicknameActor()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "update": {
                "state": "MERGED",
                "date": "2026-02-20T14:00:00Z",
                "author": {
                  "nickname": "reviewer-nick",
                  "uuid": "{reviewer-2}"
                }
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 20, 14, 0, 0, TimeSpan.Zero));
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 20, 14, 0, 0, TimeSpan.Zero));
        result.Actor.Should().NotBeNull();
        result.Actor!.Value.DisplayName.Value.Should().Be("reviewer-nick");
        result.Actor!.Value.Uuid!.Value.Should().Be("{reviewer-2}");
    }

    [Fact(DisplayName = "Map resolves merge date from merge.created_on fallback")]
    [Trait("Category", "Unit")]
    public void MapWhenMergePayloadContainsCreatedOnUsesMergeCreatedOnAsMergeDate()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "merge": {
                "created_on": "2026-02-20T15:00:00Z"
              },
              "updated_on": "2026-02-20T16:00:00Z"
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 20, 16, 0, 0, TimeSpan.Zero));
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 20, 15, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Map supports pullrequest_comment payload with updated_on fallback")]
    [Trait("Category", "Unit")]
    public void MapWhenPullRequestCommentPayloadIsProvidedMapsCommentUsingUpdatedOn()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "pullrequest_comment": {
                "updated_on": "2026-02-20T17:00:00Z",
                "user": {
                  "username": "reviewer-user"
                }
              },
              "actor": {
                "display_name": "Actor Name",
                "uuid": "{actor-1}"
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.Comment.Should().NotBeNull();
        result.Comment!.Date.Should().Be(new DateTimeOffset(2026, 2, 20, 17, 0, 0, TimeSpan.Zero));
        result.Comment.User.DisplayName.Value.Should().Be("reviewer-user");
        result.Comment.User.Uuid.Should().BeNull();
        result.Actor.Should().NotBeNull();
        result.Actor!.Value.DisplayName.Value.Should().Be("Actor Name");
    }

    [Fact(DisplayName = "Map resolves approval date from approval.date and actor from approval user")]
    [Trait("Category", "Unit")]
    public void MapWhenApprovalDateFieldIsPresentUsesApprovalDate()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "approval": {
                "date": "2026-02-20T18:00:00Z",
                "user": {
                  "display_name": "Approver"
                }
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 20, 18, 0, 0, TimeSpan.Zero));
        result.Approval.Should().NotBeNull();
        result.Approval!.Date.Should().Be(new DateTimeOffset(2026, 2, 20, 18, 0, 0, TimeSpan.Zero));
        result.Approval.User.DisplayName.Value.Should().Be("Approver");
    }

    [Fact(DisplayName = "Map falls back to root user and root date fields when nested payloads are absent")]
    [Trait("Category", "Unit")]
    public void MapWhenOnlyRootUserAndRootDateExistUsesRootFallbacks()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "user": {
                "nickname": "root-user"
              },
              "date": "2026-02-20T19:00:00Z"
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 20, 19, 0, 0, TimeSpan.Zero));
        result.Actor.Should().NotBeNull();
        result.Actor!.Value.DisplayName.Value.Should().Be("root-user");
        result.Actor!.Value.Uuid.Should().BeNull();
    }

    [Fact(DisplayName = "Map resolves activity date from pull request created_on fallback")]
    [Trait("Category", "Unit")]
    public void MapWhenPullRequestCreatedOnExistsUsesPullRequestCreatedOnAsActivityDate()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "pullrequest": {
                "created_on": "2026-02-20T20:00:00Z",
                "author": {
                  "display_name": "Author",
                  "uuid": "{author-1}"
                }
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 20, 20, 0, 0, TimeSpan.Zero));
        result.Actor.Should().NotBeNull();
        result.Actor!.Value.DisplayName.Value.Should().Be("Author");
    }

    [Fact(DisplayName = "Map resolves merged pull request fallback from updated_on when merged_on is missing")]
    [Trait("Category", "Unit")]
    public void MapWhenPullRequestMergedOnIsMissingUsesPullRequestUpdatedOn()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "pullrequest": {
                "state": "MERGED",
                "updated_on": "2026-02-20T21:00:00Z"
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 20, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Map resolves merged pull request fallback from root date when pull request dates are missing")]
    [Trait("Category", "Unit")]
    public void MapWhenPullRequestMergedDatesAreMissingUsesRootDateFallback()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "pullrequest": {
                "state": "MERGED"
              },
              "date": "2026-02-20T22:00:00Z"
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 20, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Map resolves merged pull request fallback from root created_on when date is missing")]
    [Trait("Category", "Unit")]
    public void MapWhenPullRequestMergedDateAndRootDateAreMissingUsesRootCreatedOnFallback()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "pullrequest": {
                "state": "MERGED"
              },
              "created_on": "2026-02-20T23:00:00Z"
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 20, 23, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Map resolves merged update fallback from root date when update date is missing")]
    [Trait("Category", "Unit")]
    public void MapWhenMergedUpdateDateIsMissingUsesRootDateFallback()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "update": {
                "state": "MERGED",
                "user": {
                  "display_name": "Updater"
                }
              },
              "date": "2026-02-21T00:00:00Z"
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero));
        result.Actor.Should().NotBeNull();
        result.Actor!.Value.DisplayName.Value.Should().Be("Updater");
    }

    [Fact(DisplayName = "Map resolves merge date from merge.date")]
    [Trait("Category", "Unit")]
    public void MapWhenMergeDateFieldExistsUsesMergeDate()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "merge": {
                "date": "2026-02-21T01:00:00Z"
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 21, 1, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Map supports pull_request_comment payload variant")]
    [Trait("Category", "Unit")]
    public void MapWhenPullRequestCommentWithUnderscorePayloadExistsMapsComment()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "pull_request_comment": {
                "created_on": "2026-02-21T02:00:00Z",
                "user": {
                  "display_name": "Commenter",
                  "uuid": "{commenter-1}"
                }
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.Comment.Should().NotBeNull();
        result.Comment!.Date.Should().Be(new DateTimeOffset(2026, 2, 21, 2, 0, 0, TimeSpan.Zero));
        result.Comment.User.DisplayName.Value.Should().Be("Commenter");
    }

    [Fact(DisplayName = "Map ignores comment payload when user exists but no valid comment date is available")]
    [Trait("Category", "Unit")]
    public void MapWhenCommentUserExistsWithoutDateDoesNotCreateComment()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "comment": {
                "user": {
                  "display_name": "Commenter"
                }
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.Comment.Should().BeNull();
        result.Actor.Should().NotBeNull();
        result.Actor!.Value.DisplayName.Value.Should().Be("Commenter");
    }

    [Fact(DisplayName = "Map ignores user nodes without identity fields")]
    [Trait("Category", "Unit")]
    public void MapWhenUserNodeHasNoIdentityFieldsReturnsNullActor()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "actor": {},
              "updated_on": "2026-02-21T03:00:00Z"
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.ActivityDate.Should().Be(new DateTimeOffset(2026, 2, 21, 3, 0, 0, TimeSpan.Zero));
        result.Actor.Should().BeNull();
    }

    [Fact(DisplayName = "Map resolves approval using root date fallback when approval specific dates are missing")]
    [Trait("Category", "Unit")]
    public void MapWhenApprovalDateFieldsAreMissingUsesRootDateFallback()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "approval": {
                "user": {
                  "display_name": "Approver"
                }
              },
              "date": "2026-02-21T04:00:00Z"
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.Approval.Should().NotBeNull();
        result.Approval!.Date.Should().Be(new DateTimeOffset(2026, 2, 21, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Map continues merge resolution when update state is not a string")]
    [Trait("Category", "Unit")]
    public void MapWhenUpdateStateIsNotStringStillFallsBackToMergeDate()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "update": {
                "state": 1
              },
              "merge": {
                "date": "2026-02-21T05:00:00Z"
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().Be(new DateTimeOffset(2026, 2, 21, 5, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Map returns null merge date when pull request is merged but no merge timestamps exist")]
    [Trait("Category", "Unit")]
    public void MapWhenPullRequestIsMergedWithoutAnyMergeDatesReturnsNullMergeDate()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "pullrequest": {
                "state": "MERGED"
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().BeNull();
    }

    [Fact(DisplayName = "Map returns null merge date when update is merged but no date fields exist")]
    [Trait("Category", "Unit")]
    public void MapWhenUpdateIsMergedWithoutAnyDateFieldsReturnsNullMergeDate()
    {
        // Arrange
        var mapper = new PullRequestActivityMapper();
        var activity = ParseJson(/*lang=json,strict*/ """
            {
              "update": {
                "state": "MERGED"
              }
            }
            """);

        // Act
        var result = mapper.Map(activity);

        // Assert
        result.MergeDate.Should().BeNull();
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

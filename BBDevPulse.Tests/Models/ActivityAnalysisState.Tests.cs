using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class ActivityAnalysisStateTests
{
    [Fact(DisplayName = "Constructor sets initial state from provided values")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenArgumentsAreValidSetsExpectedDefaults()
    {
        // Arrange
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var author = new DeveloperIdentity(new UserUuid("{author-1}"), new DisplayName("Author"));

        // Act
        var state = new ActivityAnalysisState(createdOn, author, shouldCalculateTtfr: true);

        // Assert
        state.CreatedOn.Should().Be(createdOn);
        state.LastActivity.Should().Be(createdOn);
        state.MergedOnFromActivity.Should().BeNull();
        state.FirstReactionOn.Should().BeNull();
        state.AuthorIdentity.Should().Be(author);
        state.ShouldCalculateTtfr.Should().BeTrue();
        state.HasActivityInRange.Should().BeFalse();
        state.HasIncludedTeamActivity.Should().BeFalse();
        state.Participants.Should().BeEmpty();
        state.CommentCounts.Should().BeEmpty();
        state.ApprovalCounts.Should().BeEmpty();
        state.TotalComments.Should().Be(0);
    }

    [Fact(DisplayName = "AddParticipant adds participant only once per identity key")]
    [Trait("Category", "Unit")]
    public void AddParticipantWhenCalledWithDuplicateKeyDoesNotOverrideExistingEntry()
    {
        // Arrange
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var state = new ActivityAnalysisState(createdOn, authorIdentity: null, shouldCalculateTtfr: false);
        var first = new DeveloperIdentity(new UserUuid("{dev-1}"), new DisplayName("Alice"));
        var secondWithSameKey = new DeveloperIdentity(new UserUuid("{DEV-1}"), new DisplayName("Changed"));

        // Act
        state.AddParticipant(first);
        state.AddParticipant(secondWithSameKey);

        // Assert
        state.Participants.Should().HaveCount(1);
        state.Participants[first.ToKey()].DisplayName.Value.Should().Be("Alice");
    }
}

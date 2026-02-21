using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PullRequestActivityTests
{
    [Fact(DisplayName = "Constructor sets all properties")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenArgumentsAreValidSetsProperties()
    {
        // Arrange
        var activityDate = new DateTimeOffset(2026, 2, 21, 9, 0, 0, TimeSpan.Zero);
        var mergeDate = activityDate.AddHours(1);
        var actor = new DeveloperIdentity(new UserUuid("{abc}"), new DisplayName("Alice"));
        var comment = new ActivityComment(actor, activityDate);
        var approval = new ActivityApproval(actor, activityDate.AddMinutes(1));

        // Act
        var activity = new PullRequestActivity(activityDate, mergeDate, actor, comment, approval);

        // Assert
        activity.ActivityDate.Should().Be(activityDate);
        activity.MergeDate.Should().Be(mergeDate);
        activity.Actor.Should().Be(actor);
        activity.Comment.Should().Be(comment);
        activity.Approval.Should().Be(approval);
    }

    [Fact(DisplayName = "IsBefore returns false when activity date is missing")]
    [Trait("Category", "Unit")]
    public void IsBeforeWhenActivityDateIsMissingReturnsFalse()
    {
        // Arrange
        var activity = new PullRequestActivity(
            activityDate: null,
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);
        var date = new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = activity.IsBefore(date);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "IsBefore returns true when activity date is earlier than provided date")]
    [Trait("Category", "Unit")]
    public void IsBeforeWhenActivityDateIsEarlierReturnsTrue()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);
        var activity = new PullRequestActivity(
            activityDate: date.AddMinutes(-5),
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.IsBefore(date);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "TryUpdateLastActivity updates when activity date is newer")]
    [Trait("Category", "Unit")]
    public void TryUpdateLastActivityWhenActivityDateIsNewerUpdatesAndReturnsTrue()
    {
        // Arrange
        var lastActivity = new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero);
        var activity = new PullRequestActivity(
            activityDate: lastActivity.AddHours(1),
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryUpdateLastActivity(ref lastActivity);

        // Assert
        result.Should().BeTrue();
        lastActivity.Should().Be(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "TryUpdateLastActivity does not update when activity date is missing")]
    [Trait("Category", "Unit")]
    public void TryUpdateLastActivityWhenActivityDateIsMissingReturnsFalse()
    {
        // Arrange
        var expectedLastActivity = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var lastActivity = expectedLastActivity;
        var activity = new PullRequestActivity(
            activityDate: null,
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryUpdateLastActivity(ref lastActivity);

        // Assert
        result.Should().BeFalse();
        lastActivity.Should().Be(expectedLastActivity);
    }

    [Fact(DisplayName = "TryUpdateLastActivity does not update when activity date is not newer")]
    [Trait("Category", "Unit")]
    public void TryUpdateLastActivityWhenActivityDateIsNotNewerReturnsFalse()
    {
        // Arrange
        var expectedLastActivity = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var lastActivity = expectedLastActivity;
        var activity = new PullRequestActivity(
            activityDate: expectedLastActivity.AddMinutes(-1),
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryUpdateLastActivity(ref lastActivity);

        // Assert
        result.Should().BeFalse();
        lastActivity.Should().Be(expectedLastActivity);
    }

    [Fact(DisplayName = "TryUpdateMergedOn updates when merge date exists and resolved merge date is empty")]
    [Trait("Category", "Unit")]
    public void TryUpdateMergedOnWhenResolvedMergeDateIsEmptyUpdatesAndReturnsTrue()
    {
        // Arrange
        DateTimeOffset? mergedOn = null;
        var mergeDate = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero);
        var activity = new PullRequestActivity(
            activityDate: null,
            mergeDate: mergeDate,
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryUpdateMergedOn(ref mergedOn);

        // Assert
        result.Should().BeTrue();
        mergedOn.Should().Be(mergeDate);
    }

    [Fact(DisplayName = "TryUpdateMergedOn does not update when merge date is missing")]
    [Trait("Category", "Unit")]
    public void TryUpdateMergedOnWhenMergeDateIsMissingReturnsFalse()
    {
        // Arrange
        DateTimeOffset? mergedOn = new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero);
        var activity = new PullRequestActivity(
            activityDate: null,
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryUpdateMergedOn(ref mergedOn);

        // Assert
        result.Should().BeFalse();
        mergedOn.Should().Be(new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "TryUpdateMergedOn updates when merge date is earlier than current resolved merge date")]
    [Trait("Category", "Unit")]
    public void TryUpdateMergedOnWhenMergeDateIsEarlierUpdatesAndReturnsTrue()
    {
        // Arrange
        DateTimeOffset? mergedOn = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero);
        var earlierMergeDate = new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero);
        var activity = new PullRequestActivity(
            activityDate: null,
            mergeDate: earlierMergeDate,
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryUpdateMergedOn(ref mergedOn);

        // Assert
        result.Should().BeTrue();
        mergedOn.Should().Be(earlierMergeDate);
    }

    [Fact(DisplayName = "TryUpdateMergedOn does not update when merge date is not earlier than current resolved merge date")]
    [Trait("Category", "Unit")]
    public void TryUpdateMergedOnWhenMergeDateIsNotEarlierReturnsFalse()
    {
        // Arrange
        var expectedMergedOn = new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero);
        DateTimeOffset? mergedOn = expectedMergedOn;
        var activity = new PullRequestActivity(
            activityDate: null,
            mergeDate: expectedMergedOn.AddHours(1),
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryUpdateMergedOn(ref mergedOn);

        // Assert
        result.Should().BeFalse();
        mergedOn.Should().Be(expectedMergedOn);
    }

    [Fact(DisplayName = "TryGetActor returns false when actor is missing")]
    [Trait("Category", "Unit")]
    public void TryGetActorWhenActorIsMissingReturnsFalse()
    {
        // Arrange
        var activity = new PullRequestActivity(
            activityDate: null,
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryGetActor(out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "TryGetActor returns true and resolved actor when actor exists")]
    [Trait("Category", "Unit")]
    public void TryGetActorWhenActorExistsReturnsTrueWithResolvedActor()
    {
        // Arrange
        var actor = new DeveloperIdentity(
            new UserUuid("{abc}"),
            new DisplayName("Alice"));
        var activity = new PullRequestActivity(
            activityDate: null,
            mergeDate: null,
            actor: actor,
            comment: null,
            approval: null);

        // Act
        var result = activity.TryGetActor(out var resolvedActor);

        // Assert
        result.Should().BeTrue();
        resolvedActor.ToKey().Should().Be(actor.ToKey());
    }
}

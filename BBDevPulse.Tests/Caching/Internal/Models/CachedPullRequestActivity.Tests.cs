using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;

namespace BBDevPulse.Tests.Caching.Internal.Models;

public sealed class CachedPullRequestActivityTests
{
    [Fact(DisplayName = "Init properties store assigned values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenAssignedReturnAssignedValues()
    {
        // Arrange
        var activityDate = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
        var actor = new CachedDeveloperIdentity { Uuid = "{reviewer-1}", DisplayName = "Reviewer" };
        var comment = new CachedActivityComment { User = actor, Date = activityDate };
        var approval = new CachedActivityApproval { User = actor, Date = activityDate.AddHours(1) };

        // Act
        var activity = new CachedPullRequestActivity
        {
            ActivityDate = activityDate,
            MergeDate = activityDate.AddDays(1),
            Actor = actor,
            Comment = comment,
            Approval = approval
        };

        // Assert
        activity.ActivityDate.Should().Be(activityDate);
        activity.MergeDate.Should().Be(activityDate.AddDays(1));
        activity.Actor.Should().BeSameAs(actor);
        activity.Comment.Should().BeSameAs(comment);
        activity.Approval.Should().BeSameAs(approval);
    }
}

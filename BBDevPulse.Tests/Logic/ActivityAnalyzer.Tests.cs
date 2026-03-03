using FluentAssertions;

using BBDevPulse.Logic;
using BBDevPulse.Models;

namespace BBDevPulse.Tests.Logic;

public sealed class ActivityAnalyzerTests
{
    [Fact(DisplayName = "Analyze throws when analysis state is null")]
    [Trait("Category", "Unit")]
    public void AnalyzeWhenAnalysisStateIsNullThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new ActivityAnalyzer();
        ActivityAnalysisState analysis = null!;
        var activity = new PullRequestActivity(null, null, null, null, null);
        var parameters = CreateParameters(DateTimeOffset.UtcNow);

        // Act
        Action act = () => analyzer.Analyze(analysis, activity, parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Analyze throws when activity is null")]
    [Trait("Category", "Unit")]
    public void AnalyzeWhenActivityIsNullThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new ActivityAnalyzer();
        var analysis = new ActivityAnalysisState(DateTimeOffset.UtcNow, null, shouldCalculateTtfr: false);
        PullRequestActivity activity = null!;
        var parameters = CreateParameters(DateTimeOffset.UtcNow);

        // Act
        Action act = () => analyzer.Analyze(analysis, activity, parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Analyze returns early when actor cannot be resolved")]
    [Trait("Category", "Unit")]
    public void AnalyzeWhenActorIsMissingSkipsCommentAndApprovalAggregation()
    {
        // Arrange
        var analyzer = new ActivityAnalyzer();
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var filterDate = createdOn.AddDays(-1);
        var parameters = CreateParameters(filterDate);
        var analysis = new ActivityAnalysisState(createdOn, authorIdentity: null, shouldCalculateTtfr: true);
        var user = CreateIdentity("{user-1}", "User");
        var activity = new PullRequestActivity(
            activityDate: createdOn.AddHours(1),
            mergeDate: createdOn.AddHours(2),
            actor: null,
            comment: new ActivityComment(user, createdOn.AddMinutes(15)),
            approval: new ActivityApproval(user, createdOn.AddMinutes(30)));

        // Act
        analyzer.Analyze(analysis, activity, parameters);

        // Assert
        analysis.LastActivity.Should().Be(createdOn.AddHours(1));
        analysis.MergedOnFromActivity.Should().Be(createdOn.AddHours(2));
        analysis.Participants.Should().BeEmpty();
        analysis.TotalComments.Should().Be(0);
        analysis.CommentCounts.Should().BeEmpty();
        analysis.ApprovalCounts.Should().BeEmpty();
        analysis.FirstReactionOn.Should().BeNull();
        analysis.HasActivityInRange.Should().BeTrue();
    }

    [Fact(DisplayName = "Analyze aggregates comment and approval counts after filter date")]
    [Trait("Category", "Unit")]
    public void AnalyzeWhenCommentAndApprovalMatchFilterUpdatesCountsAndParticipants()
    {
        // Arrange
        var analyzer = new ActivityAnalyzer();
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var filterDate = createdOn.AddDays(-1);
        var parameters = CreateParameters(filterDate);
        var author = CreateIdentity("{author-1}", "Author");
        var participant = CreateIdentity("{reviewer-1}", "Reviewer");
        var analysis = new ActivityAnalysisState(createdOn, author, shouldCalculateTtfr: true);
        var activity = new PullRequestActivity(
            activityDate: createdOn.AddHours(5),
            mergeDate: createdOn.AddHours(7),
            actor: participant,
            comment: new ActivityComment(participant, createdOn.AddHours(1)),
            approval: new ActivityApproval(participant, createdOn.AddHours(2)));

        // Act
        analyzer.Analyze(analysis, activity, parameters);

        // Assert
        analysis.LastActivity.Should().Be(createdOn.AddHours(5));
        analysis.MergedOnFromActivity.Should().Be(createdOn.AddHours(7));
        analysis.TotalComments.Should().Be(1);
        analysis.CommentCounts[participant.ToKey()].Should().Be(1);
        analysis.ApprovalCounts[participant.ToKey()].Should().Be(1);
        analysis.Participants.Should().ContainKey(participant.ToKey());
        analysis.FirstReactionOn.Should().Be(createdOn.AddHours(1));
        analysis.HasActivityInRange.Should().BeTrue();
    }

    [Fact(DisplayName = "Analyze ignores comment counts outside the configured range but still tracks participants")]
    [Trait("Category", "Unit")]
    public void AnalyzeWhenCommentIsOutsideRangeDoesNotIncrementCounts()
    {
        // Arrange
        var analyzer = new ActivityAnalyzer();
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var filterDate = createdOn.AddDays(-1);
        var parameters = CreateParameters(filterDate);
        var participant = CreateIdentity("{reviewer-1}", "Reviewer");
        var analysis = new ActivityAnalysisState(createdOn, authorIdentity: null, shouldCalculateTtfr: false);
        var activity = new PullRequestActivity(
            activityDate: createdOn.AddHours(5),
            mergeDate: null,
            actor: participant,
            comment: new ActivityComment(participant, filterDate.AddMinutes(-1)),
            approval: null);

        // Act
        analyzer.Analyze(analysis, activity, parameters);

        // Assert
        analysis.TotalComments.Should().Be(0);
        analysis.CommentCounts.Should().BeEmpty();
        analysis.FirstReactionOn.Should().BeNull();
        analysis.Participants.Should().ContainKey(participant.ToKey());
    }

    [Fact(DisplayName = "Analyze updates first reaction with earliest non-author activity only")]
    [Trait("Category", "Unit")]
    public void AnalyzeWhenShouldCalculateTtfrUsesEarliestNonAuthorReaction()
    {
        // Arrange
        var analyzer = new ActivityAnalyzer();
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var author = CreateIdentity("{author-1}", "Author");
        var reviewer = CreateIdentity("{reviewer-1}", "Reviewer");
        var filterDate = createdOn.AddDays(-1);
        var parameters = CreateParameters(filterDate);
        var analysis = new ActivityAnalysisState(createdOn, author, shouldCalculateTtfr: true);

        var approvalByAuthor = new PullRequestActivity(
            activityDate: createdOn.AddHours(1),
            mergeDate: null,
            actor: author,
            comment: null,
            approval: new ActivityApproval(author, createdOn.AddHours(1)));
        var approvalByReviewer = new PullRequestActivity(
            activityDate: createdOn.AddHours(2),
            mergeDate: null,
            actor: reviewer,
            comment: null,
            approval: new ActivityApproval(reviewer, createdOn.AddHours(2)));
        var earlierCommentByReviewer = new PullRequestActivity(
            activityDate: createdOn.AddMinutes(30),
            mergeDate: null,
            actor: reviewer,
            comment: new ActivityComment(reviewer, createdOn.AddMinutes(30)),
            approval: null);

        // Act
        analyzer.Analyze(analysis, approvalByAuthor, parameters);
        analyzer.Analyze(analysis, approvalByReviewer, parameters);
        analyzer.Analyze(analysis, earlierCommentByReviewer, parameters);

        // Assert
        analysis.FirstReactionOn.Should().Be(createdOn.AddMinutes(30));
    }

    [Fact(DisplayName = "Analyze does not update first reaction for approvals before pull request creation time")]
    [Trait("Category", "Unit")]
    public void AnalyzeWhenApprovalIsBeforeCreationDoesNotUpdateFirstReaction()
    {
        // Arrange
        var analyzer = new ActivityAnalyzer();
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var reviewer = CreateIdentity("{reviewer-1}", "Reviewer");
        var parameters = CreateParameters(createdOn.AddDays(-1));
        var analysis = new ActivityAnalysisState(
            createdOn,
            authorIdentity: null,
            shouldCalculateTtfr: true);
        var activity = new PullRequestActivity(
            activityDate: createdOn.AddHours(1),
            mergeDate: null,
            actor: reviewer,
            comment: null,
            approval: new ActivityApproval(reviewer, createdOn.AddMinutes(-1)));

        // Act
        analyzer.Analyze(analysis, activity, parameters);

        // Assert
        analysis.FirstReactionOn.Should().BeNull();
    }

    [Fact(DisplayName = "Analyze ignores activity timestamps after the configured upper bound")]
    [Trait("Category", "Unit")]
    public void AnalyzeWhenActivityIsAfterUpperBoundDoesNotAggregateInRangeValues()
    {
        // Arrange
        var analyzer = new ActivityAnalyzer();
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var fromDate = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
        var toDateExclusive = new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);
        var reviewer = CreateIdentity("{reviewer-1}", "Reviewer");
        var analysis = new ActivityAnalysisState(createdOn, authorIdentity: null, shouldCalculateTtfr: true);
        var activity = new PullRequestActivity(
            activityDate: toDateExclusive.AddHours(1),
            mergeDate: toDateExclusive.AddHours(2),
            actor: reviewer,
            comment: new ActivityComment(reviewer, toDateExclusive.AddMinutes(10)),
            approval: new ActivityApproval(reviewer, toDateExclusive.AddMinutes(20)));

        // Act
        analyzer.Analyze(analysis, activity, CreateParameters(fromDate, toDateExclusive));

        // Assert
        analysis.HasActivityInRange.Should().BeFalse();
        analysis.LastActivity.Should().Be(createdOn);
        analysis.MergedOnFromActivity.Should().BeNull();
        analysis.TotalComments.Should().Be(0);
        analysis.CommentCounts.Should().BeEmpty();
        analysis.ApprovalCounts.Should().BeEmpty();
        analysis.FirstReactionOn.Should().BeNull();
        analysis.Participants.Should().ContainKey(reviewer.ToKey());
    }

    private static DeveloperIdentity CreateIdentity(string uuid, string displayName)
    {
        return new DeveloperIdentity(new UserUuid(uuid), new DisplayName(displayName));
    }

    private static ReportParameters CreateParameters(
        DateTimeOffset filterDate,
        DateTimeOffset? toDateExclusive = null)
    {
        return new ReportParameters(
            filterDate,
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            [],
            toDateExclusive: toDateExclusive);
    }
}

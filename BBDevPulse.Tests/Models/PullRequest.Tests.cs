using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PullRequestTests
{
    [Fact(DisplayName = "Constructor sets properties")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenArgumentsAreValidSetsProperties()
    {
        // Arrange
        var id = new PullRequestId(1);
        var createdOn = new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero);
        var updatedOn = createdOn.AddHours(1);
        var mergedOn = createdOn.AddHours(2);
        var closedOn = createdOn.AddHours(3);
        var author = new User(new DisplayName("Alice"), new UserUuid("{uuid}"));
        var destination = new PullRequestDestination(new PullRequestBranch("develop"));

        // Act
        var pullRequest = new PullRequest(
            id,
            PullRequestState.Merged,
            closedOn,
            createdOn,
            updatedOn,
            mergedOn,
            author,
            destination);

        // Assert
        pullRequest.Id.Should().Be(id);
        pullRequest.State.Should().Be(PullRequestState.Merged);
        pullRequest.ClosedOn.Should().Be(closedOn);
        pullRequest.CreatedOn.Should().Be(createdOn);
        pullRequest.UpdatedOn.Should().Be(updatedOn);
        pullRequest.MergedOn.Should().Be(mergedOn);
        pullRequest.Author.Should().Be(author);
        pullRequest.Destination.Should().Be(destination);
    }

    [Fact(DisplayName = "Should stop by time filter in created-on-only mode when pull request is older than filter date")]
    [Trait("Category", "Unit")]
    public void ShouldStopByTimeFilterWhenCreatedOnOnlyAndPullRequestIsOlderReturnsTrue()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var pullRequest = CreatePullRequest(
            createdOn: filterDate.AddDays(-1),
            updatedOn: filterDate.AddDays(2));

        // Act
        var result = pullRequest.ShouldStopByTimeFilter(filterDate, PrTimeFilterMode.CreatedOnOnly);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "Should not stop by time filter in last-known-update mode when updated date is newer than filter date")]
    [Trait("Category", "Unit")]
    public void ShouldStopByTimeFilterWhenLastKnownUpdateModeAndUpdatedDateIsRecentReturnsFalse()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var pullRequest = CreatePullRequest(
            createdOn: filterDate.AddDays(-10),
            updatedOn: filterDate.AddDays(1));

        // Act
        var result = pullRequest.ShouldStopByTimeFilter(filterDate, PrTimeFilterMode.LastKnownUpdateAndCreated);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "Should stop by time filter in last-known-update mode when created and updated dates are older than filter date")]
    [Trait("Category", "Unit")]
    public void ShouldStopByTimeFilterWhenLastKnownUpdateModeAndCreatedAndUpdatedAreOldReturnsTrue()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var pullRequest = CreatePullRequest(
            createdOn: filterDate.AddDays(-10),
            updatedOn: filterDate.AddDays(-5));

        // Act
        var result = pullRequest.ShouldStopByTimeFilter(filterDate, PrTimeFilterMode.LastKnownUpdateAndCreated);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "Should stop by time filter throws when filter mode is unknown")]
    [Trait("Category", "Unit")]
    public void ShouldStopByTimeFilterWhenFilterModeIsUnknownThrowsNotImplementedException()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var pullRequest = CreatePullRequest(createdOn: filterDate);

        // Act
        Action act = () => _ = pullRequest.ShouldStopByTimeFilter(filterDate, (PrTimeFilterMode)999);

        // Assert
        act.Should().Throw<NotImplementedException>();
    }

    [Fact(DisplayName = "Matches branch filter throws when branch list is null")]
    [Trait("Category", "Unit")]
    public void MatchesBranchFilterWhenBranchListIsNullThrowsArgumentNullException()
    {
        // Arrange
        var pullRequest = CreatePullRequest(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero));
        IReadOnlyList<BranchName> branchList = null!;

        // Act
        Action act = () => _ = pullRequest.MatchesBranchFilter(branchList);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Matches branch filter returns true when filter list is empty")]
    [Trait("Category", "Unit")]
    public void MatchesBranchFilterWhenFilterListIsEmptyReturnsTrue()
    {
        // Arrange
        var pullRequest = CreatePullRequest(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = pullRequest.MatchesBranchFilter([]);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "Matches branch filter returns false when destination branch is missing")]
    [Trait("Category", "Unit")]
    public void MatchesBranchFilterWhenDestinationBranchIsMissingReturnsFalse()
    {
        // Arrange
        var pullRequest = CreatePullRequest(
            createdOn: new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            destinationBranch: null);
        var branchList = new List<BranchName> { new("develop") };

        // Act
        var result = pullRequest.MatchesBranchFilter(branchList);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "Matches branch filter compares branch names case-insensitively")]
    [Trait("Category", "Unit")]
    public void MatchesBranchFilterWhenTargetBranchMatchesIgnoringCaseReturnsTrue()
    {
        // Arrange
        var pullRequest = CreatePullRequest(
            createdOn: new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            destinationBranch: "Develop");
        var branchList = new List<BranchName> { new("develop") };

        // Act
        var result = pullRequest.MatchesBranchFilter(branchList);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "Resolve rejected on returns null for non-rejected states")]
    [Trait("Category", "Unit")]
    public void ResolveRejectedOnWhenStateIsNotRejectedReturnsNull()
    {
        // Arrange
        var pullRequest = CreatePullRequest(
            createdOn: new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            state: PullRequestState.Merged,
            closedOn: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = pullRequest.ResolveRejectedOn();

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "Resolve rejected on prefers closed date for declined pull requests")]
    [Trait("Category", "Unit")]
    public void ResolveRejectedOnWhenDeclinedAndClosedDateExistsReturnsClosedDate()
    {
        // Arrange
        var closedOn = new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);
        var pullRequest = CreatePullRequest(
            createdOn: new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            state: PullRequestState.Declined,
            closedOn: closedOn,
            updatedOn: closedOn.AddHours(2));

        // Act
        var result = pullRequest.ResolveRejectedOn();

        // Assert
        result.Should().Be(closedOn);
    }

    [Fact(DisplayName = "Resolve rejected on falls back to updated date when closed date is missing")]
    [Trait("Category", "Unit")]
    public void ResolveRejectedOnWhenRejectedAndClosedDateMissingReturnsUpdatedDate()
    {
        // Arrange
        var updatedOn = new DateTimeOffset(2026, 2, 21, 3, 0, 0, TimeSpan.Zero);
        var pullRequest = CreatePullRequest(
            createdOn: new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            state: PullRequestState.Superseded,
            updatedOn: updatedOn);

        // Act
        var result = pullRequest.ResolveRejectedOn();

        // Assert
        result.Should().Be(updatedOn);
    }

    [Fact(DisplayName = "Should calculate TTFR when pull request was created on or after filter date")]
    [Trait("Category", "Unit")]
    public void ShouldCalculateTtfrWhenCreatedOnOrAfterFilterDateReturnsTrue()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
        var pullRequest = CreatePullRequest(createdOn: filterDate);

        // Act
        var result = pullRequest.ShouldCalculateTtfr(filterDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "Should not calculate TTFR when pull request was created before filter date")]
    [Trait("Category", "Unit")]
    public void ShouldCalculateTtfrWhenCreatedBeforeFilterDateReturnsFalse()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
        var pullRequest = CreatePullRequest(createdOn: filterDate.AddMinutes(-1));

        // Act
        var result = pullRequest.ShouldCalculateTtfr(filterDate);

        // Assert
        result.Should().BeFalse();
    }

    private static PullRequest CreatePullRequest(
        DateTimeOffset createdOn,
        DateTimeOffset? updatedOn = null,
        PullRequestState state = PullRequestState.Open,
        DateTimeOffset? closedOn = null,
        DateTimeOffset? mergedOn = null,
        string? destinationBranch = "develop")
    {
        var destination = destinationBranch is null
            ? null
            : new PullRequestDestination(new PullRequestBranch(destinationBranch));

        return new PullRequest(
            new PullRequestId(1),
            state,
            closedOn,
            createdOn,
            updatedOn,
            mergedOn,
            author: null,
            destination);
    }
}

using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PullRequestReportTests
{
    [Fact(DisplayName = "Constructor throws when repository is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRepositoryIsNullThrowsArgumentNullException()
    {
        // Arrange
        string repository = null!;

        // Act
        Action act = () => _ = Create(
            repository: repository,
            repositorySlug: "slug",
            author: "author",
            targetBranch: "develop");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when repository slug is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRepositorySlugIsNullThrowsArgumentNullException()
    {
        // Arrange
        string repositorySlug = null!;

        // Act
        Action act = () => _ = Create(
            repository: "repo",
            repositorySlug: repositorySlug,
            author: "author",
            targetBranch: "develop");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when author is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenAuthorIsNullThrowsArgumentNullException()
    {
        // Arrange
        string author = null!;

        // Act
        Action act = () => _ = Create(
            repository: "repo",
            repositorySlug: "slug",
            author: author,
            targetBranch: "develop");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when target branch is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTargetBranchIsNullThrowsArgumentNullException()
    {
        // Arrange
        string targetBranch = null!;

        // Act
        Action act = () => _ = Create(
            repository: "repo",
            repositorySlug: "slug",
            author: "author",
            targetBranch: targetBranch);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor sets all properties")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenArgumentsAreValidSetsProperties()
    {
        // Arrange
        var createdOn = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
        var lastActivity = createdOn.AddHours(2);
        var mergedOn = createdOn.AddHours(5);
        var rejectedOn = createdOn.AddHours(6);
        var firstReactionOn = createdOn.AddMinutes(30);
        var id = new PullRequestId(99);

        // Act
        var report = new PullRequestReport(
            repository: "repo",
            repositorySlug: "slug",
            author: "author",
            targetBranch: "develop",
            createdOn: createdOn,
            lastActivity: lastActivity,
            mergedOn: mergedOn,
            rejectedOn: rejectedOn,
            state: PullRequestState.Merged,
            id: id,
            comments: 7,
            corrections: 3,
            firstReactionOn: firstReactionOn);

        // Assert
        report.Repository.Should().Be("repo");
        report.RepositorySlug.Should().Be("slug");
        report.Author.Should().Be("author");
        report.TargetBranch.Should().Be("develop");
        report.CreatedOn.Should().Be(createdOn);
        report.LastActivity.Should().Be(lastActivity);
        report.MergedOn.Should().Be(mergedOn);
        report.RejectedOn.Should().Be(rejectedOn);
        report.State.Should().Be(PullRequestState.Merged);
        report.Id.Should().Be(id);
        report.Comments.Should().Be(7);
        report.Corrections.Should().Be(3);
        report.FirstReactionOn.Should().Be(firstReactionOn);
    }

    private static PullRequestReport Create(
        string repository,
        string repositorySlug,
        string author,
        string targetBranch)
    {
        return new PullRequestReport(
            repository,
            repositorySlug,
            author,
            targetBranch,
            createdOn: new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero),
            lastActivity: new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero),
            mergedOn: null,
            rejectedOn: null,
            state: PullRequestState.Open,
            id: new PullRequestId(1),
            comments: 0,
            firstReactionOn: null);
    }
}

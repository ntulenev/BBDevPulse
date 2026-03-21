using FluentAssertions;

using BBDevPulse.API;
using BBDevPulse.Models;

namespace BBDevPulse.Tests.API;

public sealed class PullRequestCommitRangeCacheTests
{
    [Fact(DisplayName = "TryGet throws when workspace is null")]
    [Trait("Category", "Unit")]
    public void TryGetWhenWorkspaceIsNullThrowsArgumentNullException()
    {
        // Arrange
        var cache = new PullRequestCommitRangeCache();
        Workspace workspace = null!;

        // Act
        Action act = () => _ = cache.TryGet(workspace, new RepoSlug("repo"), 1, out _, out _);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Store throws when repository slug is null")]
    [Trait("Category", "Unit")]
    public void StoreWhenRepositorySlugIsNullThrowsArgumentNullException()
    {
        // Arrange
        var cache = new PullRequestCommitRangeCache();
        RepoSlug repoSlug = null!;

        // Act
        Action act = () => cache.Store(new Workspace("ws"), repoSlug, 1, "source", "destination");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Store and TryGet return cached commit hashes")]
    [Trait("Category", "Unit")]
    public void StoreWhenCommitHashesArePresentCachesRange()
    {
        // Arrange
        var cache = new PullRequestCommitRangeCache();

        // Act
        cache.Store(new Workspace("ws"), new RepoSlug("repo"), 7, "source-hash", "destination-hash");
        var found = cache.TryGet(new Workspace("ws"), new RepoSlug("repo"), 7, out var sourceCommitHash, out var destinationCommitHash);

        // Assert
        found.Should().BeTrue();
        sourceCommitHash.Should().Be("source-hash");
        destinationCommitHash.Should().Be("destination-hash");
    }

    [Fact(DisplayName = "Store ignores incomplete commit hash ranges")]
    [Trait("Category", "Unit")]
    public void StoreWhenCommitHashesAreIncompleteDoesNotCacheRange()
    {
        // Arrange
        var cache = new PullRequestCommitRangeCache();

        // Act
        cache.Store(new Workspace("ws"), new RepoSlug("repo"), 7, "source-hash", null);
        var found = cache.TryGet(new Workspace("ws"), new RepoSlug("repo"), 7, out var sourceCommitHash, out var destinationCommitHash);

        // Assert
        found.Should().BeFalse();
        sourceCommitHash.Should().BeNull();
        destinationCommitHash.Should().BeNull();
    }

    [Fact(DisplayName = "Cache keys are isolated by workspace repository and pull request")]
    [Trait("Category", "Unit")]
    public void TryGetWhenDifferentWorkspaceOrRepositoryOrPullRequestReturnsCachedEntryOnlyForExactMatch()
    {
        // Arrange
        var cache = new PullRequestCommitRangeCache();
        cache.Store(new Workspace("ws"), new RepoSlug("repo"), 7, "source-hash", "destination-hash");

        // Act
        var wrongWorkspace = cache.TryGet(new Workspace("ws-2"), new RepoSlug("repo"), 7, out _, out _);
        var wrongRepository = cache.TryGet(new Workspace("ws"), new RepoSlug("repo-2"), 7, out _, out _);
        var wrongPullRequest = cache.TryGet(new Workspace("ws"), new RepoSlug("repo"), 8, out _, out _);
        var exactMatch = cache.TryGet(new Workspace("ws"), new RepoSlug("repo"), 7, out var sourceCommitHash, out var destinationCommitHash);

        // Assert
        wrongWorkspace.Should().BeFalse();
        wrongRepository.Should().BeFalse();
        wrongPullRequest.Should().BeFalse();
        exactMatch.Should().BeTrue();
        sourceCommitHash.Should().Be("source-hash");
        destinationCommitHash.Should().Be("destination-hash");
    }
}

using FluentAssertions;

using BBDevPulse.Caching.Internal.Models;
using BBDevPulse.Caching.Mappers;
using BBDevPulse.Models;

namespace BBDevPulse.Tests.Caching.Mappers;

public sealed class PullRequestAnalysisCacheMapperTests
{
    [Fact(DisplayName = "Map throws when snapshot is null")]
    [Trait("Category", "Unit")]
    public void MapWhenSnapshotIsNullThrowsArgumentNullException()
    {
        // Arrange
        var mapper = new PullRequestAnalysisCacheMapper();
        PullRequestAnalysisSnapshot snapshot = null!;

        // Act
        Action act = () => _ = mapper.Map(snapshot);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "TryMap throws when cached snapshot is null")]
    [Trait("Category", "Unit")]
    public void TryMapWhenCachedSnapshotIsNullThrowsArgumentNullException()
    {
        // Arrange
        var mapper = new PullRequestAnalysisCacheMapper();
        CachedPullRequestAnalysisSnapshot snapshot = null!;

        // Act
        Action act = () => _ = mapper.TryMap(snapshot, out _);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Map converts domain snapshot to cached snapshot")]
    [Trait("Category", "Unit")]
    public void MapWhenSnapshotIsValidReturnsCachedSnapshot()
    {
        // Arrange
        var mapper = new PullRequestAnalysisCacheMapper();
        var reviewer = new DeveloperIdentity(new UserUuid("{reviewer-1}"), new DisplayName("Reviewer"));
        var snapshot = new PullRequestAnalysisSnapshot(
            [
                new PullRequestActivity(
                    new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
                    null,
                    reviewer,
                    new ActivityComment(reviewer, new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero)),
                    null)
            ],
            [
                new PullRequestCommitInfo(
                    "commit-1",
                    new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
                    "Fix cache mapping")
            ],
            new PullRequestSizeSummary(3, 25, 5),
            [
                new DeveloperCommitActivity(
                    "Repo",
                    "repo",
                    new PullRequestId(42),
                    "commit-1",
                    "Fix cache mapping",
                    new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
                    new PullRequestSizeSummary(1, 5, 2))
            ],
            hasEnrichment: true);

        // Act
        var cachedSnapshot = mapper.Map(snapshot);

        // Assert
        cachedSnapshot.Activities.Should().ContainSingle();
        cachedSnapshot.Activities![0].Actor!.DisplayName.Should().Be("Reviewer");
        cachedSnapshot.CorrectionCommits.Should().ContainSingle();
        cachedSnapshot.CorrectionCommits![0].Hash.Should().Be("commit-1");
        cachedSnapshot.SizeSummary.Should().NotBeNull();
        cachedSnapshot.SizeSummary!.FilesChanged.Should().Be(3);
        cachedSnapshot.CommitActivities.Should().ContainSingle();
        cachedSnapshot.CommitActivities![0].CommitHash.Should().Be("commit-1");
        cachedSnapshot.HasEnrichment.Should().BeTrue();
    }

    [Fact(DisplayName = "TryMap converts cached snapshot to domain snapshot")]
    [Trait("Category", "Unit")]
    public void TryMapWhenCachedSnapshotIsValidReturnsDomainSnapshot()
    {
        // Arrange
        var mapper = new PullRequestAnalysisCacheMapper();
        var cachedSnapshot = new CachedPullRequestAnalysisSnapshot
        {
            Activities =
            [
                new CachedPullRequestActivity
                {
                    ActivityDate = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
                    Actor = new CachedDeveloperIdentity
                    {
                        Uuid = "{reviewer-1}",
                        DisplayName = "Reviewer"
                    },
                    Comment = new CachedActivityComment
                    {
                        User = new CachedDeveloperIdentity
                        {
                            Uuid = "{reviewer-1}",
                            DisplayName = "Reviewer"
                        },
                        Date = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero)
                    }
                }
            ],
            CorrectionCommits =
            [
                new CachedPullRequestCommitInfo
                {
                    Hash = "commit-1",
                    Date = new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
                    Message = "Fix cache mapping"
                }
            ],
            SizeSummary = new CachedPullRequestSizeSummary
            {
                FilesChanged = 3,
                LinesAdded = 25,
                LinesRemoved = 5
            },
            CommitActivities =
            [
                new CachedDeveloperCommitActivity
                {
                    Repository = "Repo",
                    RepositorySlug = "repo",
                    PullRequestId = 42,
                    CommitHash = "commit-1",
                    Message = "Fix cache mapping",
                    Date = new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
                    SizeSummary = new CachedPullRequestSizeSummary
                    {
                        FilesChanged = 1,
                        LinesAdded = 5,
                        LinesRemoved = 2
                    }
                }
            ],
            HasEnrichment = true
        };

        // Act
        var mapped = mapper.TryMap(cachedSnapshot, out var snapshot);

        // Assert
        mapped.Should().BeTrue();
        snapshot.Activities.Should().ContainSingle();
        snapshot.Activities[0].Comment.Should().NotBeNull();
        snapshot.Activities[0].Comment!.User.DisplayName.Value.Should().Be("Reviewer");
        snapshot.CorrectionCommits.Should().ContainSingle();
        snapshot.CorrectionCommits[0].Hash.Should().Be("commit-1");
        snapshot.SizeSummary.LineChurn.Should().Be(30);
        snapshot.CommitActivities.Should().ContainSingle();
        snapshot.CommitActivities[0].CommitHash.Should().Be("commit-1");
        snapshot.HasEnrichment.Should().BeTrue();
    }

    [Fact(DisplayName = "TryMap returns false when cached snapshot is incomplete")]
    [Trait("Category", "Unit")]
    public void TryMapWhenCachedSnapshotIsIncompleteReturnsFalse()
    {
        // Arrange
        var mapper = new PullRequestAnalysisCacheMapper();
        var cachedSnapshot = new CachedPullRequestAnalysisSnapshot
        {
            Activities = null,
            CorrectionCommits = [],
            SizeSummary = new CachedPullRequestSizeSummary(),
            CommitActivities = []
        };

        // Act
        var mapped = mapper.TryMap(cachedSnapshot, out var snapshot);

        // Assert
        mapped.Should().BeFalse();
        snapshot.Should().BeNull();
    }
}

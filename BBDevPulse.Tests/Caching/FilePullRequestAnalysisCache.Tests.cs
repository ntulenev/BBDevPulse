using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Caching;
using BBDevPulse.Caching.Mappers;
using BBDevPulse.Models;

using Moq;

namespace BBDevPulse.Tests.Caching;

public sealed class FilePullRequestAnalysisCacheTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), "BBDevPulse.Tests", Guid.NewGuid().ToString("N"));

    [Fact(DisplayName = "Constructor throws when mapper is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenMapperIsNullThrowsArgumentNullException()
    {
        // Arrange
        PullRequestAnalysisCacheMapper mapper = null!;
        var telemetryService = new Mock<IBitbucketTelemetryService>(MockBehavior.Strict).Object;

        // Act
        Action act = () => _ = new FilePullRequestAnalysisCache(mapper, telemetryService, _cacheDirectory);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when telemetry service is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTelemetryServiceIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketTelemetryService telemetryService = null!;

        // Act
        Action act = () => _ = new FilePullRequestAnalysisCache(new PullRequestAnalysisCacheMapper(), telemetryService, _cacheDirectory);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Store and TryGet round-trip cached pull request analysis snapshot")]
    [Trait("Category", "Unit")]
    public void StoreWhenSnapshotIsValidPersistsAndReturnsSnapshot()
    {
        // Arrange
        var telemetryService = CreateTelemetryService();
        var cache = new FilePullRequestAnalysisCache(new PullRequestAnalysisCacheMapper(), telemetryService.Object, _cacheDirectory);
        var workspace = new Workspace("ws");
        var repoSlug = new RepoSlug("repo");
        var pullRequestId = new PullRequestId(42);
        const string pullRequestFingerprint = "pr-fingerprint";
        const string parametersFingerprint = "params-fingerprint";
        var reviewer = new DeveloperIdentity(new UserUuid("{reviewer-1}"), new DisplayName("Reviewer"));
        var snapshot = new PullRequestAnalysisSnapshot(
            activities:
            [
                new PullRequestActivity(
                    activityDate: new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
                    mergeDate: null,
                    actor: reviewer,
                    comment: new ActivityComment(reviewer, new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero)),
                    approval: null)
            ],
            correctionCommits:
            [
                new PullRequestCommitInfo(
                    "commit-1",
                    new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
                    "Cached fix")
            ],
            sizeSummary: new PullRequestSizeSummary(FilesChanged: 3, LinesAdded: 25, LinesRemoved: 5),
            commitActivities:
            [
                new DeveloperCommitActivity(
                    "Repo",
                    "repo",
                    pullRequestId,
                    "commit-1",
                    "Cached fix",
                    new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero),
                    new PullRequestSizeSummary(FilesChanged: 1, LinesAdded: 5, LinesRemoved: 1))
            ],
            hasEnrichment: true);

        // Act
        cache.Store(workspace, repoSlug, pullRequestId, pullRequestFingerprint, parametersFingerprint, snapshot);
        var found = cache.TryGet(
            workspace,
            repoSlug,
            pullRequestId,
            pullRequestFingerprint,
            parametersFingerprint,
            out var cachedSnapshot);

        // Assert
        found.Should().BeTrue();
        cachedSnapshot.Activities.Should().HaveCount(1);
        cachedSnapshot.Activities[0].Comment.Should().NotBeNull();
        cachedSnapshot.Activities[0].Comment!.User.DisplayName.Value.Should().Be("Reviewer");
        cachedSnapshot.CorrectionCommits.Should().ContainSingle();
        cachedSnapshot.CorrectionCommits[0].Hash.Should().Be("commit-1");
        cachedSnapshot.SizeSummary.LineChurn.Should().Be(30);
        cachedSnapshot.CommitActivities.Should().ContainSingle();
        cachedSnapshot.CommitActivities[0].CommitHash.Should().Be("commit-1");
        telemetryService.Verify(x => x.TrackAnalysisSnapshotCacheStore(), Times.Once);
        telemetryService.Verify(x => x.TrackAnalysisSnapshotCacheHit(It.IsAny<PullRequestAnalysisSnapshot>()), Times.Once);
    }

    [Fact(DisplayName = "TryGet returns false when cache file does not exist")]
    [Trait("Category", "Unit")]
    public void TryGetWhenCacheFileDoesNotExistReturnsFalse()
    {
        // Arrange
        var telemetryService = CreateTelemetryService();
        var cache = new FilePullRequestAnalysisCache(new PullRequestAnalysisCacheMapper(), telemetryService.Object, _cacheDirectory);

        // Act
        var found = cache.TryGet(
            new Workspace("ws"),
            new RepoSlug("repo"),
            new PullRequestId(42),
            "pr-fingerprint",
            "params-fingerprint",
            out var snapshot);

        // Assert
        found.Should().BeFalse();
        snapshot.Should().BeNull();
        telemetryService.Verify(x => x.TrackAnalysisSnapshotCacheMiss(), Times.Once);
    }

    [Fact(DisplayName = "Store cleans up temporary file after successful write")]
    [Trait("Category", "Unit")]
    public void StoreWhenWriteSucceedsDeletesTemporaryFile()
    {
        // Arrange
        var telemetryService = CreateTelemetryService();
        var cache = new FilePullRequestAnalysisCache(new PullRequestAnalysisCacheMapper(), telemetryService.Object, _cacheDirectory);
        var snapshot = new PullRequestAnalysisSnapshot([], [], PullRequestSizeSummary.Empty, [], hasEnrichment: false);

        // Act
        cache.Store(
            new Workspace("ws"),
            new RepoSlug("repo"),
            new PullRequestId(42),
            "pr-fingerprint",
            "params-fingerprint",
            snapshot);

        // Assert
        Directory.EnumerateFiles(_cacheDirectory, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
        telemetryService.Verify(x => x.TrackAnalysisSnapshotCacheStore(), Times.Once);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private static Mock<IBitbucketTelemetryService> CreateTelemetryService()
    {
        var telemetryService = new Mock<IBitbucketTelemetryService>(MockBehavior.Strict);
        telemetryService.Setup(x => x.TrackAnalysisSnapshotCacheHit(It.IsAny<PullRequestAnalysisSnapshot>()));
        telemetryService.Setup(x => x.TrackAnalysisSnapshotCacheMiss());
        telemetryService.Setup(x => x.TrackAnalysisSnapshotCacheStore());
        telemetryService.Setup(x => x.TrackRequest(It.IsAny<Uri>()));
        telemetryService.Setup(x => x.GetSnapshot()).Returns(new BitbucketTelemetrySnapshot(false, 0, 0, 0, 0, 0, []));
        return telemetryService;
    }
}

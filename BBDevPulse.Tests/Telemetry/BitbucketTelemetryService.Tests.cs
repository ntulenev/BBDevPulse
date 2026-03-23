using BBDevPulse.Configuration;
using BBDevPulse.Models;
using BBDevPulse.Telemetry;

using FluentAssertions;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Tests.Telemetry;

public sealed class BitbucketTelemetryServiceTests
{
    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new BitbucketTelemetryService(options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "TrackRequest aggregates requests by normalized Bitbucket API")]
    [Trait("Category", "Unit")]
    public void TrackRequestWhenTelemetryEnabledAggregatesRequests()
    {
        // Arrange
        var service = new BitbucketTelemetryService(Options.Create(CreateOptions(enabled: true)));

        // Act
        service.TrackRequest(new Uri("https://api.bitbucket.org/2.0/user", UriKind.Absolute));
        service.TrackRequest(new Uri("https://api.bitbucket.org/2.0/repositories/workspace/repo-one/pullrequests/101/activity?pagelen=50", UriKind.Absolute));
        service.TrackRequest(new Uri("https://api.bitbucket.org/2.0/repositories/workspace/repo-two/pullrequests/202/activity?pagelen=50", UriKind.Absolute));
        service.TrackRequest(new Uri("https://api.bitbucket.org/2.0/repositories/workspace/repo-two/pullrequests/202/commits", UriKind.Absolute));
        service.TrackRequest(new Uri("https://api.bitbucket.org/2.0/repositories/workspace/repo-two/diffstat/revision?from=1&to=2", UriKind.Absolute));
        service.TrackRequest(new Uri("https://api.bitbucket.org/2.0/repositories/workspace/repo-two/pullrequests/202", UriKind.Absolute));
        service.TrackRequest(new Uri("https://api.bitbucket.org/2.0/repositories/workspace/repo-two/pullrequests?state=OPEN", UriKind.Absolute));

        // Assert
        service.GetSnapshot().Should().BeEquivalentTo(new BitbucketTelemetrySnapshot(
            true,
            7,
            0,
            0,
            0,
            0,
            [
                new BitbucketApiRequestStatistic("GET /repositories/{workspace}/{repository}/pullrequests/{pullRequestId}/activity", 2),
                new BitbucketApiRequestStatistic("GET /repositories/{workspace}/{repository}/diffstat", 1),
                new BitbucketApiRequestStatistic("GET /repositories/{workspace}/{repository}/pullrequests", 1),
                new BitbucketApiRequestStatistic("GET /repositories/{workspace}/{repository}/pullrequests/{pullRequestId}", 1),
                new BitbucketApiRequestStatistic("GET /repositories/{workspace}/{repository}/pullrequests/{pullRequestId}/commits", 1),
                new BitbucketApiRequestStatistic("GET /user", 1)
            ]));
    }

    [Fact(DisplayName = "Cache telemetry tracks hits misses stores and avoided requests")]
    [Trait("Category", "Unit")]
    public void CacheTelemetryWhenTelemetryEnabledTracksSnapshotReuse()
    {
        // Arrange
        var service = new BitbucketTelemetryService(Options.Create(CreateOptions(enabled: true)));
        var snapshot = new PullRequestAnalysisSnapshot(
            activities: [],
            correctionCommits:
            [
                new PullRequestCommitInfo("commit-1", new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero), "Fix")
            ],
            sizeSummary: new PullRequestSizeSummary(FilesChanged: 3, LinesAdded: 10, LinesRemoved: 4),
            commitActivities:
            [
                new DeveloperCommitActivity(
                    "Repo",
                    "repo",
                    new PullRequestId(42),
                    "commit-1",
                    "Fix",
                    new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
                    new PullRequestSizeSummary(FilesChanged: 1, LinesAdded: 2, LinesRemoved: 1))
            ],
            hasEnrichment: true);

        // Act
        service.TrackAnalysisSnapshotCacheHit(snapshot);
        service.TrackAnalysisSnapshotCacheMiss();
        service.TrackAnalysisSnapshotCacheStore();

        // Assert
        var telemetry = service.GetSnapshot();
        telemetry.AnalysisSnapshotCacheHits.Should().Be(1);
        telemetry.AnalysisSnapshotCacheMisses.Should().Be(1);
        telemetry.AnalysisSnapshotCacheStores.Should().Be(1);
        telemetry.EstimatedAvoidedRequests.Should().Be(4);
    }

    [Fact(DisplayName = "Track methods do nothing when telemetry is disabled")]
    [Trait("Category", "Unit")]
    public void TrackMethodsWhenTelemetryDisabledReturnEmptySnapshot()
    {
        // Arrange
        var service = new BitbucketTelemetryService(Options.Create(CreateOptions(enabled: false)));
        var snapshot = new PullRequestAnalysisSnapshot([], [], PullRequestSizeSummary.Empty, [], hasEnrichment: false);

        // Act
        service.TrackRequest(new Uri("https://api.bitbucket.org/2.0/user", UriKind.Absolute));
        service.TrackAnalysisSnapshotCacheHit(snapshot);
        service.TrackAnalysisSnapshotCacheMiss();
        service.TrackAnalysisSnapshotCacheStore();

        // Assert
        service.GetSnapshot().Should().BeEquivalentTo(new BitbucketTelemetrySnapshot(false, 0, 0, 0, 0, 0, []));
    }

    private static BitbucketOptions CreateOptions(bool enabled)
    {
        return new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 25,
            PullRequestConcurrency = 1,
            RepositoryConcurrency = 1,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Pdf = new PdfOptions(),
            Telemetry = new BitbucketTelemetryOptions
            {
                Enabled = enabled
            }
        };
    }
}

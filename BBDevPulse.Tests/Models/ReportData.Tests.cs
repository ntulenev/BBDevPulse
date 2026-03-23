using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class ReportDataTests
{
    [Fact(DisplayName = "Constructor throws when parameters are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenParametersAreNullThrowsArgumentNullException()
    {
        // Arrange
        ReportParameters parameters = null!;

        // Act
        Action act = () => _ = new ReportData(parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor stores parameters")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenParametersAreValidStoresParameters()
    {
        // Arrange
        var parameters = CreateReportParameters();

        // Act
        var reportData = new ReportData(parameters);

        // Assert
        reportData.Parameters.Should().BeSameAs(parameters);
    }

    [Fact(DisplayName = "IsDeveloperIncluded returns true for matching team member when team filter is active")]
    [Trait("Category", "Unit")]
    public void IsDeveloperIncludedWhenTeamMatchesReturnsTrue()
    {
        // Arrange
        var parameters = CreateReportParameters(teamFilter: "Core");
        var reportData = new ReportData(
            parameters,
            new Dictionary<DisplayName, PersonCsvRow>
            {
                [new DisplayName("Alice")] = new("Senior", "Core")
            });
        var identity = new DeveloperIdentity(null, new DisplayName("Alice"));

        // Act
        var result = reportData.IsDeveloperIncluded(identity);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "IsDeveloperIncluded returns false for missing or mismatched team member when team filter is active")]
    [Trait("Category", "Unit")]
    public void IsDeveloperIncludedWhenTeamDoesNotMatchReturnsFalse()
    {
        // Arrange
        var parameters = CreateReportParameters(teamFilter: "Core");
        var reportData = new ReportData(
            parameters,
            new Dictionary<DisplayName, PersonCsvRow>
            {
                [new DisplayName("Bob")] = new("Senior", "Other")
            });

        // Act
        var missingResult = reportData.IsDeveloperIncluded(new DeveloperIdentity(null, new DisplayName("Alice")));
        var mismatchedResult = reportData.IsDeveloperIncluded(new DeveloperIdentity(null, new DisplayName("Bob")));

        // Assert
        missingResult.Should().BeFalse();
        mismatchedResult.Should().BeFalse();
    }

    [Fact(DisplayName = "GetOrAddDeveloper returns existing stats for the same UUID regardless of case")]
    [Trait("Category", "Unit")]
    public void GetOrAddDeveloperWhenUuidMatchesIgnoringCaseReturnsExistingStats()
    {
        // Arrange
        var reportData = new ReportData(CreateReportParameters());
        var firstIdentity = new DeveloperIdentity(
            new UserUuid("{ABC-123}"),
            new DisplayName("Alice"));
        var secondIdentity = new DeveloperIdentity(
            new UserUuid("{abc-123}"),
            new DisplayName("Alice Updated"));

        // Act
        var firstStats = reportData.GetOrAddDeveloper(firstIdentity);
        var secondStats = reportData.GetOrAddDeveloper(secondIdentity);

        // Assert
        secondStats.Should().BeSameAs(firstStats);
        secondStats.DisplayName.Value.Should().Be("Alice");
        secondStats.BitbucketUuid!.Value.Should().Be("{ABC-123}");
        reportData.DeveloperStats.Should().HaveCount(1);
    }

    [Fact(DisplayName = "GetOrAddDeveloper creates and returns stats for a new developer identity")]
    [Trait("Category", "Unit")]
    public void GetOrAddDeveloperWhenDeveloperIsNewCreatesStats()
    {
        // Arrange
        var reportData = new ReportData(CreateReportParameters());
        var identity = new DeveloperIdentity(
            uuid: null,
            displayName: new DisplayName("Bob"));

        // Act
        var stats = reportData.GetOrAddDeveloper(identity);

        // Assert
        stats.DisplayName.Value.Should().Be("Bob");
        stats.BitbucketUuid.Should().BeNull();
        reportData.DeveloperStats.Should().HaveCount(1);
    }

    [Fact(DisplayName = "GetOrAddDeveloper updates missing UUID when same developer is seen later with UUID")]
    [Trait("Category", "Unit")]
    public void GetOrAddDeveloperWhenUuidArrivesLaterStoresItOnExistingStats()
    {
        // Arrange
        var reportData = new ReportData(CreateReportParameters());
        var withoutUuid = new DeveloperIdentity(null, new DisplayName("Alice"));
        var withUuid = new DeveloperIdentity(new UserUuid("{alice-1}"), new DisplayName("Alice"));

        // Act
        var firstStats = reportData.GetOrAddDeveloper(withoutUuid);
        var secondStats = reportData.GetOrAddDeveloper(withUuid);

        // Assert
        secondStats.Should().BeSameAs(firstStats);
        secondStats.BitbucketUuid!.Value.Should().Be("{alice-1}");
    }

    [Fact(DisplayName = "SortReportsByCreatedOn sorts reports in ascending creation order")]
    [Trait("Category", "Unit")]
    public void SortReportsByCreatedOnWhenReportsAreOutOfOrderSortsAscending()
    {
        // Arrange
        var reportData = new ReportData(CreateReportParameters());
        var newerDate = new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero);
        var olderDate = newerDate.AddHours(-4);

        reportData.Reports.Add(CreatePullRequestReport(id: 2, createdOn: newerDate));
        reportData.Reports.Add(CreatePullRequestReport(id: 1, createdOn: olderDate));

        // Act
        reportData.SortReportsByCreatedOn();

        // Assert
        reportData.Reports.Select(entry => entry.Id.Value).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "Throughput counters include only in-range metric pull requests")]
    [Trait("Category", "Unit")]
    public void ThroughputCountersWhenReportsExistCountOnlyMetricRowsInsideRange()
    {
        // Arrange
        var reportData = new ReportData(new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            workspace: new Workspace("workspace"),
            repoNameFilter: new RepoNameFilter(""),
            repoNameList: [],
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: [],
            toDateExclusive: new DateTimeOffset(2026, 2, 25, 0, 0, 0, TimeSpan.Zero)));

        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            targetBranch: "develop",
            createdOn: new DateTimeOffset(2026, 2, 21, 10, 0, 0, TimeSpan.Zero),
            lastActivity: new DateTimeOffset(2026, 2, 21, 11, 0, 0, TimeSpan.Zero),
            mergedOn: new DateTimeOffset(2026, 2, 24, 10, 0, 0, TimeSpan.Zero),
            rejectedOn: new DateTimeOffset(2026, 2, 23, 10, 0, 0, TimeSpan.Zero),
            state: PullRequestState.Merged,
            id: new PullRequestId(1),
            comments: 0));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoB",
            repositorySlug: "repob",
            author: "Bob",
            targetBranch: "develop",
            createdOn: new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero),
            lastActivity: new DateTimeOffset(2026, 2, 24, 11, 0, 0, TimeSpan.Zero),
            mergedOn: new DateTimeOffset(2026, 2, 24, 10, 0, 0, TimeSpan.Zero),
            rejectedOn: new DateTimeOffset(2026, 2, 24, 12, 0, 0, TimeSpan.Zero),
            state: PullRequestState.Merged,
            id: new PullRequestId(2),
            comments: 0));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoC",
            repositorySlug: "repoc",
            author: "Carol",
            targetBranch: "develop",
            createdOn: new DateTimeOffset(2026, 2, 22, 10, 0, 0, TimeSpan.Zero),
            lastActivity: new DateTimeOffset(2026, 2, 22, 11, 0, 0, TimeSpan.Zero),
            mergedOn: new DateTimeOffset(2026, 2, 26, 10, 0, 0, TimeSpan.Zero),
            rejectedOn: new DateTimeOffset(2026, 2, 26, 10, 0, 0, TimeSpan.Zero),
            state: PullRequestState.Merged,
            id: new PullRequestId(3),
            comments: 0));
        reportData.Reports.Add(new PullRequestReport(
            repository: "RepoD",
            repositorySlug: "repod",
            author: "External",
            targetBranch: "develop",
            createdOn: new DateTimeOffset(2026, 2, 23, 10, 0, 0, TimeSpan.Zero),
            lastActivity: new DateTimeOffset(2026, 2, 23, 11, 0, 0, TimeSpan.Zero),
            mergedOn: new DateTimeOffset(2026, 2, 24, 12, 0, 0, TimeSpan.Zero),
            rejectedOn: new DateTimeOffset(2026, 2, 24, 12, 0, 0, TimeSpan.Zero),
            state: PullRequestState.Merged,
            id: new PullRequestId(4),
            comments: 0,
            isActivityOnlyMatch: true));

        // Act
        var created = reportData.PullRequestsCreatedInRange;
        var merged = reportData.PullRequestsMergedInRange;
        var rejected = reportData.PullRequestsRejectedInRange;

        // Assert
        created.Should().Be(2);
        merged.Should().Be(2);
        rejected.Should().Be(2);
    }

    [Fact(DisplayName = "GetOpenedPullRequestCountsPerDeveloper returns ordered authored counts and skips zeros")]
    [Trait("Category", "Unit")]
    public void GetOpenedPullRequestCountsPerDeveloperWhenDeveloperStatsExistReturnsOrderedNonZeroCounts()
    {
        // Arrange
        var reportData = new ReportData(CreateReportParameters());
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Alice")))] =
            new DeveloperStats(new DisplayName("Alice")) { PrsOpenedSince = 3 };
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Bob")))] =
            new DeveloperStats(new DisplayName("Bob")) { PrsOpenedSince = 0 };
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Carol")))] =
            new DeveloperStats(new DisplayName("Carol")) { PrsOpenedSince = 1 };

        // Act
        var counts = reportData.GetOpenedPullRequestCountsPerDeveloper();

        // Assert
        counts.Should().Equal(1d, 3d);
    }

    [Fact(DisplayName = "GetPeerCommentCountsPerDeveloper returns ordered counts including zeros")]
    [Trait("Category", "Unit")]
    public void GetPeerCommentCountsPerDeveloperWhenDeveloperStatsExistReturnsOrderedCounts()
    {
        // Arrange
        var reportData = new ReportData(CreateReportParameters());
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Alice")))] =
            new DeveloperStats(new DisplayName("Alice")) { PeerCommentsAfter = 3 };
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Bob")))] =
            new DeveloperStats(new DisplayName("Bob")) { PeerCommentsAfter = 0 };
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(null, new DisplayName("Carol")))] =
            new DeveloperStats(new DisplayName("Carol")) { PeerCommentsAfter = 1 };

        // Act
        var counts = reportData.GetPeerCommentCountsPerDeveloper();

        // Assert
        counts.Should().Equal(0d, 1d, 3d);
    }

    private static PullRequestReport CreatePullRequestReport(int id, DateTimeOffset createdOn)
    {
        return new PullRequestReport(
            repository: "BBDevPulse",
            repositorySlug: "bbdevpulse",
            author: "Alice",
            targetBranch: "develop",
            createdOn: createdOn,
            lastActivity: createdOn,
            mergedOn: null,
            rejectedOn: null,
            state: PullRequestState.Open,
            id: new PullRequestId(id),
            comments: 0,
            firstReactionOn: null);
    }

    private static ReportParameters CreateReportParameters(string? teamFilter = null)
    {
        return new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            workspace: new Workspace("workspace"),
            repoNameFilter: new RepoNameFilter(""),
            repoNameList: [],
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: [],
            teamFilter: teamFilter);
    }
}

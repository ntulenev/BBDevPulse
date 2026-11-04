using FluentAssertions;

using BBDevPulse.Models;
using BBDevPulse.Math;

namespace BBDevPulse.Tests.Models;

public sealed class ReportDataTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Core")]
    public void DefaultIdentityIsRejectedAtEachBoundary(string? teamFilter)
    {
        var data = new ReportData(CreateReportParameters(teamFilter: teamFilter));
        Assert.Throws<ArgumentNullException>("identity", () => DeveloperKey.FromIdentity(default));
        Assert.Throws<ArgumentNullException>("identity", () => data.IsDeveloperIncluded(default));
        Assert.Throws<ArgumentNullException>("identity", () => data.ObserveDeveloperIdentity(default));
        Assert.Throws<ArgumentNullException>("identity", () => data.GetOrAddDeveloper(default));
        Assert.Empty(data.DeveloperStats);
    }

    [Fact]
    public void ConcurrentReadersSeeTheSamePublishedMetrics()
    {
        var data = new ReportData(CreateReportParameters());
        var identity = new DeveloperIdentity(new UserUuid("A"), new DisplayName("Alice"));
        var stats = data.GetOrAddDeveloper(identity);
        var created = data.Parameters.FilterDate;
        var report = CreatePullRequestReport(1, created, created.AddHours(6));
        data.Reports.Add(report);
        stats.AuthoredPullRequests.Add(report);
        data.Freeze();

        Parallel.For(0, 128, _ =>
        {
            Assert.Equal(1, data.PullRequestsCreatedInRange);
            Assert.Same(report, Assert.Single(data.DeveloperStats[DeveloperKey.FromIdentity(identity)].AuthoredPullRequests));
            Assert.Equal(0.25, data.GetTtfrStatistics(new StatisticsCalculator()).MedianDays);
        });
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(1, int.MaxValue, 1)]
    public void DiffstatRejectsUnrepresentableSizes(int files, int additions, int removals)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PullRequestSizeSummary(files, additions, removals));
    }

    [Fact]
    public void RemovingAReportAndChangingCalculatorAffectTtfr()
    {
        var data = new ReportData(CreateReportParameters());
        var created = DateTimeOffset.UnixEpoch;
        data.Reports.Add(CreatePullRequestReport(1, created, created.AddDays(1)));
        data.Reports.Add(CreatePullRequestReport(2, created, created.AddDays(3)));
        data.GetTtfrStatistics(new StatisticsCalculator()).Count.Should().Be(2);
        data.Reports.RemoveAt(1);
        var calculator = new Moq.Mock<BBDevPulse.Abstractions.IStatisticsCalculator>();
        calculator.Setup(x => x.Percentile(Moq.It.IsAny<IReadOnlyList<double>>(), 50)).Returns(0.5);
        var result = data.GetTtfrStatistics(calculator.Object);
        result.Count.Should().Be(1);
        result.MedianDays.Should().Be(0.5);
    }

    [Fact]
    public void ParallelAnalysisUpdatesArePreservedOnPublication()
    {
        var data = new ReportData(CreateReportParameters());
        var identity = new DeveloperIdentity(new UserUuid("alice"), new DisplayName("Alice"));
        Parallel.For(0, 200, _ => data.ApplyAnalysis(() => data.GetOrAddDeveloper(identity).CommentsAfter++));
        data.Freeze();
        data.DeveloperStats.Values.Single().CommentsAfter.Should().Be(200);
    }

    [Fact]
    public void PublishedReportIsIndependentOfCollectionReferencesHeldByProducer()
    {
        var data = new ReportData(CreateReportParameters());
        var list = data.Reports;
        var stats = data.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("alice"), new DisplayName("Alice")));
        var details = stats.AuthoredPullRequests;
        var report = CreatePullRequestReport(1, DateTimeOffset.UnixEpoch);
        list.Add(report);
        details.Add(report);
        data.Freeze();

        list.Clear();
        details.Clear();
        data.Reports.Should().ContainSingle();
        stats.AuthoredPullRequests.Should().ContainSingle();
        Assert.Throws<NotSupportedException>(() => data.Reports.Clear());
        Assert.Throws<InvalidOperationException>(() => stats.CommentsAfter++);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameNameWithDistinctUuidsDoesNotMergeDevelopers(bool reverse)
    {
        var data = new ReportData(CreateReportParameters());
        var first = new DeveloperIdentity(new UserUuid("{B}"), new DisplayName("Alice"));
        var second = new DeveloperIdentity(new UserUuid("{a}"), new DisplayName("ALICE"));
        var identities = reverse ? new[] { second, first } : new[] { first, second };
        foreach (var identity in identities)
        {
            data.GetOrAddDeveloper(identity).CommentsAfter++;
        }
        data.DeveloperStats.Should().HaveCount(2);
        data.DeveloperStats.Values.Should().OnlyContain(stats => stats.CommentsAfter == 1);
        data.GetIdentityCollisions().Should().ContainSingle();
    }

    [Fact]
    public void SameUuidAndMissingUuidDoNotCreateCollision()
    {
        var data = new ReportData(CreateReportParameters());
        data.ObserveDeveloperIdentity(new DeveloperIdentity(new UserUuid("{a}"), new DisplayName("Alice")));
        data.ObserveDeveloperIdentity(new DeveloperIdentity(new UserUuid("{A}"), new DisplayName("alice")));
        data.ObserveDeveloperIdentity(new DeveloperIdentity(null, new DisplayName("Alice")));
        data.GetIdentityCollisions().Should().BeEmpty();
    }
    [Fact]
    public void GetTtfrStatisticsSharesThreeObservationSummary()
    {
        var data = new ReportData(CreateReportParameters());
        var created = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        data.Reports.Add(CreatePullRequestReport(1, created, created));
        data.Reports.Add(CreatePullRequestReport(2, created, created.AddHours(2)));
        data.Reports.Add(CreatePullRequestReport(3, created, created.AddHours(4)));
        data.Reports.Add(CreatePullRequestReport(4, created));

        var statistics = data.GetTtfrStatistics(new StatisticsCalculator());

        statistics.Count.Should().Be(3);
        statistics.MedianDays.Should().BeApproximately(2d / 24, 0.000001);
        statistics.P75Days.Should().BeApproximately(3d / 24, 0.000001);
        data.GetTtfrStatistics(new StatisticsCalculator()).Should().BeEquivalentTo(statistics);
    }

    [Fact]
    public void GetTtfrStatisticsReturnsEmptySummary()
    {
        var data = new ReportData(CreateReportParameters());
        var statistics = data.GetTtfrStatistics(new StatisticsCalculator());
        statistics.Count.Should().Be(0);
        statistics.MedianDays.Should().Be(0);
        statistics.P75Days.Should().Be(0);
    }

    [Fact]
    public void GetTtfrStatisticsReflectsReportsAddedAfterPreviousCalculation()
    {
        var data = new ReportData(CreateReportParameters());
        var calculator = new StatisticsCalculator();
        data.GetTtfrStatistics(calculator).Count.Should().Be(0);
        var created = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        data.Reports.Add(CreatePullRequestReport(1, created, created.AddHours(2)));
        data.GetTtfrStatistics(calculator).Count.Should().Be(1);
    }

    [Fact(DisplayName = "TTFR statistics count a forced included Saturday")]
    public void GetTtfrStatisticsCountsForcedIncludedSaturday()
    {
        var data = new ReportData(CreateReportParameters(
            excludeWeekend: true,
            forcedIncludeDates: [new DateOnly(2026, 11, 7)]));
        var created = new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
        data.Reports.Add(CreatePullRequestReport(1, created, created.AddDays(1)));

        var statistics = data.GetTtfrStatistics(new StatisticsCalculator());

        statistics.Count.Should().Be(1);
        statistics.MedianDays.Should().BeApproximately(15d / 24, 0.000001);
    }

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
        var identity = new DeveloperIdentity(new UserUuid("alice"), new DisplayName("Alice"));

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
        var missingResult = reportData.IsDeveloperIncluded(new DeveloperIdentity(new UserUuid("alice"), new DisplayName("Alice")));
        var mismatchedResult = reportData.IsDeveloperIncluded(new DeveloperIdentity(new UserUuid("bob"), new DisplayName("Bob")));

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
            uuid: new UserUuid("bob"),
            displayName: new DisplayName("Bob"));

        // Act
        var stats = reportData.GetOrAddDeveloper(identity);

        // Assert
        stats.DisplayName.Value.Should().Be("Bob");
        stats.BitbucketUuid!.Value.Should().Be("bob");
        reportData.DeveloperStats.Should().HaveCount(1);
    }

    [Fact]
    public void MissingUuidIsReportedAndCannotCreateStats()
    {
        var reportData = new ReportData(CreateReportParameters());
        var identity = new DeveloperIdentity(null, new DisplayName("Alice"));
        reportData.ObserveDeveloperIdentity(identity);
        Assert.True(reportData.HasMissingUserUuid);
        Assert.Throws<ArgumentException>(() => reportData.GetOrAddDeveloper(identity));
        Assert.Empty(reportData.DeveloperStats);
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

    [Fact(DisplayName = "GetOpenedPullRequestCountsPerDeveloper includes developers with zero opened PRs")]
    [Trait("Category", "Unit")]
    public void GetOpenedPullRequestCountsPerDeveloperWhenDeveloperStatsExistIncludesZeroCounts()
    {
        // Arrange
        var reportData = new ReportData(CreateReportParameters());
        var alice = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("alice"), new DisplayName("Alice")));
        alice.PrsOpenedSince = 3;
        var bob = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("bob"), new DisplayName("Bob")));
        bob.PrsOpenedSince = 0;
        var carol = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("carol"), new DisplayName("Carol")));
        carol.PrsOpenedSince = 1;

        // Act
        var counts = reportData.GetOpenedPullRequestCountsPerDeveloper();

        // Assert
        counts.Should().Equal(0d, 1d, 3d);
    }

    [Fact]
    public void OpenedPullRequestDistributionCalculatesMedianAcrossAllObservedDevelopers()
    {
        var data = new ReportData(CreateReportParameters());
        foreach (var (name, count) in new[] { ("Alice", 3), ("Bob", 0), ("Carol", 0) })
        {
            data.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid(name), new DisplayName(name))).PrsOpenedSince = count;
        }

        var counts = data.GetOpenedPullRequestCountsPerDeveloper();
        counts.Should().Equal(0d, 0d, 3d);
        new StatisticsCalculator().Percentile(counts, 50).Should().Be(0);
    }

    [Fact]
    public void OpenedPullRequestDistributionIncludesReviewersWhenNoPrsWereOpened()
    {
        var data = new ReportData(CreateReportParameters());
        data.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("reviewer"), new DisplayName("Reviewer"))).CommentsAfter = 1;

        var counts = data.GetOpenedPullRequestCountsPerDeveloper();
        counts.Should().Equal(0d);
        new StatisticsCalculator().Percentile(counts, 50).Should().Be(0);
    }

    [Fact(DisplayName = "GetPeerCommentCountsPerDeveloper returns ordered counts including zeros")]
    [Trait("Category", "Unit")]
    public void GetPeerCommentCountsPerDeveloperWhenDeveloperStatsExistReturnsOrderedCounts()
    {
        // Arrange
        var reportData = new ReportData(CreateReportParameters());
        var alice = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("alice"), new DisplayName("Alice")));
        alice.PeerCommentsAfter = 3;
        var bob = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("bob"), new DisplayName("Bob")));
        bob.PeerCommentsAfter = 0;
        var carol = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("carol"), new DisplayName("Carol")));
        carol.PeerCommentsAfter = 1;

        // Act
        var counts = reportData.GetPeerCommentCountsPerDeveloper();

        // Assert
        counts.Should().Equal(0d, 1d, 3d);
    }

    private static PullRequestReport CreatePullRequestReport(int id, DateTimeOffset createdOn, DateTimeOffset? firstReactionOn = null)
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
            firstReactionOn: firstReactionOn);
    }

    private static ReportParameters CreateReportParameters(
        string? teamFilter = null,
        bool excludeWeekend = false,
        IReadOnlyList<DateOnly>? forcedIncludeDates = null)
    {
        return new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            workspace: new Workspace("workspace"),
            repoNameFilter: new RepoNameFilter(""),
            repoNameList: [],
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: [],
            excludeWeekend: excludeWeekend,
            teamFilter: teamFilter,
            forcedIncludeDates: forcedIncludeDates);
    }
}

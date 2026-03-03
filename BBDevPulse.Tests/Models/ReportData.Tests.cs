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
        reportData.DeveloperStats.Should().HaveCount(1);
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

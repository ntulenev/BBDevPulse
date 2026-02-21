using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class RepositoryTests
{
    [Fact(DisplayName = "Constructor throws when name is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenNameIsNullThrowsArgumentNullException()
    {
        // Arrange
        RepoName name = null!;
        var slug = new RepoSlug("repo");

        // Act
        Action act = () => _ = new Repository(name, slug);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when slug is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenSlugIsNullThrowsArgumentNullException()
    {
        // Arrange
        var name = new RepoName("Repo");
        RepoSlug slug = null!;

        // Act
        Action act = () => _ = new Repository(name, slug);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "DisplayName returns repository name")]
    [Trait("Category", "Unit")]
    public void DisplayNameWhenNameIsPresentReturnsName()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));

        // Act
        var result = repository.DisplayName;

        // Assert
        result.Should().Be("BBDevPulse");
    }

    [Fact(DisplayName = "MatchesFilter throws when parameters are null")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenParametersAreNullThrowsArgumentNullException()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        ReportParameters parameters = null!;

        // Act
        Action act = () => _ = repository.MatchesFilter(parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "MatchesFilter in list mode returns true when filter list is empty")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenListModeAndFilterListIsEmptyReturnsTrue()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            repoNameList: []);

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "MatchesFilter in list mode matches repository slug case-insensitively")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenListModeAndSlugMatchesIgnoringCaseReturnsTrue()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            repoNameList: [new RepoName("BBDEVPULSE")]);

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "MatchesFilter in list mode returns false when neither name nor slug matches")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenListModeAndNoEntryMatchesReturnsFalse()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            repoNameList: [new RepoName("OtherRepo")]);

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "MatchesFilter throws in search-by-filter mode")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenSearchByFilterModeThrowsNotImplementedException()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.SearchByFilter,
            repoNameFilter: "pulse");

        // Act
        Action act = () => _ = repository.MatchesFilter(parameters);

        // Assert
        act.Should().Throw<NotImplementedException>();
    }

    [Fact(DisplayName = "MatchesFilter in fallback mode applies contains match by name or slug")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenSearchModeIsUnknownAppliesContainsFilter()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        var parameters = CreateReportParameters(
            repoSearchMode: (RepoSearchMode)999,
            repoNameFilter: "pulse");

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeTrue();
    }

    private static ReportParameters CreateReportParameters(
        RepoSearchMode repoSearchMode,
        IReadOnlyList<RepoName>? repoNameList = null,
        string repoNameFilter = "")
    {
        return new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            workspace: new Workspace("workspace"),
            repoNameFilter: new RepoNameFilter(repoNameFilter),
            repoNameList: repoNameList ?? [],
            repoSearchMode: repoSearchMode,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: []);
    }
}

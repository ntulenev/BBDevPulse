using FluentAssertions;

using BBDevPulse.Models;

using System.Reflection;
using System.Runtime.CompilerServices;

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

    [Fact(DisplayName = "DisplayName falls back to slug when repository name is whitespace in invalid state")]
    [Trait("Category", "Unit")]
    public void DisplayNameWhenNameValueIsWhitespaceReturnsSlug()
    {
        // Arrange
        var invalidName = CreateRepoNameWithoutValidation(" ");
        var repository = new Repository(invalidName, new RepoSlug("bbdevpulse"));

        // Act
        var result = repository.DisplayName;

        // Assert
        result.Should().Be("bbdevpulse");
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
        var repository = new Repository(new RepoName("RepositoryName"), new RepoSlug("bbdevpulse-slug"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            repoNameList: [new RepoName("BBDEVPULSE-SLUG")]);

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "MatchesFilter in list mode matches repository name case-insensitively")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenListModeAndNameMatchesIgnoringCaseReturnsTrue()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("repository-slug"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            repoNameList: [new RepoName("bbdevpulse")]);

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

    [Fact(DisplayName = "MatchesFilter in search-by-filter mode returns true when filter is blank")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenSearchByFilterModeAndFilterIsBlankReturnsTrue()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.SearchByFilter,
            repoNameFilter: " ");

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "MatchesFilter in search-by-filter mode matches repository name by contains (case-insensitive)")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenSearchByFilterModeAndNameContainsFilterReturnsTrue()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("repository-slug"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.SearchByFilter,
            repoNameFilter: "pulse");

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "MatchesFilter in search-by-filter mode matches repository slug by contains (case-insensitive)")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenSearchByFilterModeAndSlugContainsFilterReturnsTrue()
    {
        // Arrange
        var repository = new Repository(new RepoName("Repository"), new RepoSlug("bbdevpulse-slug"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.SearchByFilter,
            repoNameFilter: "PULSE");

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "MatchesFilter in search-by-filter mode returns false when filter does not match name or slug")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenSearchByFilterModeAndFilterDoesNotMatchReturnsFalse()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        var parameters = CreateReportParameters(
            repoSearchMode: RepoSearchMode.SearchByFilter,
            repoNameFilter: "other");

        // Act
        var result = repository.MatchesFilter(parameters);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "MatchesFilter throws when search mode is unknown")]
    [Trait("Category", "Unit")]
    public void MatchesFilterWhenSearchModeIsUnknownThrowsNotImplementedException()
    {
        // Arrange
        var repository = new Repository(new RepoName("BBDevPulse"), new RepoSlug("bbdevpulse"));
        var parameters = CreateReportParameters(
            repoSearchMode: (RepoSearchMode)999,
            repoNameFilter: "pulse");

        // Act
        Action act = () => _ = repository.MatchesFilter(parameters);

        // Assert
        act.Should().Throw<NotImplementedException>();
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

    private static RepoName CreateRepoNameWithoutValidation(string value)
    {
        var repoName = (RepoName)RuntimeHelpers.GetUninitializedObject(typeof(RepoName));
        var field = typeof(RepoName).GetField(
            "<Value>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(repoName, value);
        return repoName;
    }
}

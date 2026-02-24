using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class ReportParametersTests
{
    [Fact(DisplayName = "Constructor throws when workspace is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenWorkspaceIsNullThrowsArgumentNullException()
    {
        // Arrange
        Workspace workspace = null!;

        // Act
        Action act = () => _ = new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            workspace: workspace,
            repoNameFilter: new RepoNameFilter(string.Empty),
            repoNameList: [],
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when repository name filter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRepoNameFilterIsNullThrowsArgumentNullException()
    {
        // Arrange
        RepoNameFilter repoNameFilter = null!;

        // Act
        Action act = () => _ = new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            workspace: new Workspace("workspace"),
            repoNameFilter: repoNameFilter,
            repoNameList: [],
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when repository name list is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRepoNameListIsNullThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<RepoName> repoNameList = null!;

        // Act
        Action act = () => _ = new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            workspace: new Workspace("workspace"),
            repoNameFilter: new RepoNameFilter(string.Empty),
            repoNameList: repoNameList,
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when branch name list is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenBranchNameListIsNullThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<BranchName> branchNameList = null!;

        // Act
        Action act = () => _ = new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            workspace: new Workspace("workspace"),
            repoNameFilter: new RepoNameFilter(string.Empty),
            repoNameList: [],
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: branchNameList);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor sets all properties")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenArgumentsAreValidSetsProperties()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);
        var workspace = new Workspace("workspace");
        var repoNameFilter = new RepoNameFilter("pulse");
        IReadOnlyList<RepoName> repoNameList = [new RepoName("Repo1")];
        IReadOnlyList<BranchName> branchNameList = [new BranchName("develop")];
        IReadOnlyList<DateOnly> excludedDays = [new DateOnly(2026, 2, 22)];

        // Act
        var parameters = new ReportParameters(
            filterDate,
            workspace,
            repoNameFilter,
            repoNameList,
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.LastKnownUpdateAndCreated,
            branchNameList,
            excludeWeekend: true,
            excludedDays: excludedDays,
            pullRequestSizeMode: PullRequestSizeMode.Files);

        // Assert
        parameters.FilterDate.Should().Be(filterDate);
        parameters.Workspace.Should().Be(workspace);
        parameters.RepoNameFilter.Should().Be(repoNameFilter);
        parameters.RepoNameList.Should().BeSameAs(repoNameList);
        parameters.RepoSearchMode.Should().Be(RepoSearchMode.FilterFromTheList);
        parameters.PrTimeFilterMode.Should().Be(PrTimeFilterMode.LastKnownUpdateAndCreated);
        parameters.BranchNameList.Should().BeSameAs(branchNameList);
        parameters.ExcludeWeekend.Should().BeTrue();
        parameters.ExcludedDays.Should().Contain(new DateOnly(2026, 2, 22));
        parameters.PullRequestSizeMode.Should().Be(PullRequestSizeMode.Files);
    }

}

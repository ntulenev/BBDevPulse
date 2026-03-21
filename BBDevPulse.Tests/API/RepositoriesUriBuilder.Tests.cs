using FluentAssertions;

using BBDevPulse.API;
using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

using Moq;

namespace BBDevPulse.Tests.API;

public sealed class RepositoriesUriBuilderTests
{
    [Fact(DisplayName = "Constructor throws when URI builder is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenUriBuilderIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketUriBuilder uriBuilder = null!;

        // Act
        Action act = () => _ = new RepositoriesUriBuilder(
            uriBuilder,
            Options.Create(CreateOptions()));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new RepositoriesUriBuilder(
            new Mock<IBitbucketUriBuilder>(MockBehavior.Strict).Object,
            options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Build throws when workspace is null")]
    [Trait("Category", "Unit")]
    public void BuildWhenWorkspaceIsNullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new RepositoriesUriBuilder(
            new Mock<IBitbucketUriBuilder>(MockBehavior.Strict).Object,
            Options.Create(CreateOptions()));
        Workspace workspace = null!;

        // Act
        Action act = () => _ = builder.Build(workspace);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Build uses repository list field group without q when no repo filter is configured")]
    [Trait("Category", "Unit")]
    public void BuildWhenRepositoryFilterIsEmptyBuildsBasePath()
    {
        // Arrange
        var uriBuilder = new Mock<IBitbucketUriBuilder>(MockBehavior.Strict);
        const string expectedPath = "repositories/ws?pagelen=25";
        uriBuilder.Setup(x => x.BuildRelativeUri(
                It.Is<string>(path => path == expectedPath),
                It.Is<BitbucketFieldGroup>(group => group == BitbucketFieldGroup.RepositoryList)))
            .Returns(new Uri(expectedPath, UriKind.Relative));

        var builder = new RepositoriesUriBuilder(uriBuilder.Object, Options.Create(CreateOptions()));

        // Act
        var result = builder.Build(new Workspace("ws"));

        // Assert
        result.ToString().Should().Be(expectedPath);
        uriBuilder.VerifyAll();
    }

    [Fact(DisplayName = "Build adds search filter query for repository name and slug")]
    [Trait("Category", "Unit")]
    public void BuildWhenSearchByFilterModeConfiguredBuildsExpectedQuery()
    {
        // Arrange
        var uriBuilder = new Mock<IBitbucketUriBuilder>(MockBehavior.Strict);
        const string expectedPath =
            "repositories/ws?pagelen=50&q=%28name%20~%20%22dev%5C%22pulse%5C%5Capi%22%20OR%20slug%20~%20%22dev%5C%22pulse%5C%5Capi%22%29";
        uriBuilder.Setup(x => x.BuildRelativeUri(
                It.Is<string>(path => path == expectedPath),
                It.Is<BitbucketFieldGroup>(group => group == BitbucketFieldGroup.RepositoryList)))
            .Returns(new Uri(expectedPath, UriKind.Relative));

        var builder = new RepositoriesUriBuilder(
            uriBuilder.Object,
            Options.Create(CreateOptions(
                pageLength: 50,
                repoSearchMode: RepoSearchMode.SearchByFilter,
                repoNameFilter: "dev\"pulse\\api")));

        // Act
        var result = builder.Build(new Workspace("ws"));

        // Assert
        result.ToString().Should().Be(expectedPath);
        uriBuilder.VerifyAll();
    }

    [Fact(DisplayName = "Build adds exact match query for repository names from configured list")]
    [Trait("Category", "Unit")]
    public void BuildWhenFilterFromTheListModeConfiguredBuildsExpectedQuery()
    {
        // Arrange
        var uriBuilder = new Mock<IBitbucketUriBuilder>(MockBehavior.Strict);
        const string expectedPath =
            "repositories/ws?pagelen=25&q=%28name%20%3D%20%22Repo.One%22%20OR%20slug%20%3D%20%22Repo.One%22%20OR%20name%20%3D%20%22repo-two%22%20OR%20slug%20%3D%20%22repo-two%22%29";
        uriBuilder.Setup(x => x.BuildRelativeUri(
                It.Is<string>(path => path == expectedPath),
                It.Is<BitbucketFieldGroup>(group => group == BitbucketFieldGroup.RepositoryList)))
            .Returns(new Uri(expectedPath, UriKind.Relative));

        var builder = new RepositoriesUriBuilder(
            uriBuilder.Object,
            Options.Create(CreateOptions(
                repoSearchMode: RepoSearchMode.FilterFromTheList,
                repoNameList: ["Repo.One", "repo-two", "REPO-TWO"])));

        // Act
        var result = builder.Build(new Workspace("ws"));

        // Assert
        result.ToString().Should().Be(expectedPath);
        uriBuilder.VerifyAll();
    }

    private static BitbucketOptions CreateOptions(
        int pageLength = 25,
        RepoSearchMode repoSearchMode = RepoSearchMode.FilterFromTheList,
        string repoNameFilter = "",
        string[]? repoNameList = null)
    {
        return new BitbucketOptions
        {
            Days = 7,
            Workspace = "ws",
            FromDate = null,
            ToDate = null,
            PageLength = pageLength,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = repoNameFilter,
            RepoNameList = repoNameList ?? [],
            BranchNameList = [],
            RepoSearchMode = repoSearchMode,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Pdf = new PdfOptions()
        };
    }
}

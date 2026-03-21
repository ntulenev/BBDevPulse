using FluentAssertions;

using BBDevPulse.API;
using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

using Moq;
using System.Globalization;

namespace BBDevPulse.Tests.API;

public sealed class PullRequestsUriBuilderTests
{
    [Fact(DisplayName = "Constructor throws when URI builder is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenUriBuilderIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketUriBuilder uriBuilder = null!;

        // Act
        Action act = () => _ = new PullRequestsUriBuilder(
            uriBuilder,
            Options.Create(CreateOptions(new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero))));

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
        Action act = () => _ = new PullRequestsUriBuilder(new Mock<IBitbucketUriBuilder>(MockBehavior.Strict).Object, options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Build throws when workspace is null")]
    [Trait("Category", "Unit")]
    public void BuildWhenWorkspaceIsNullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PullRequestsUriBuilder(
            new Mock<IBitbucketUriBuilder>(MockBehavior.Strict).Object,
            Options.Create(CreateOptions(new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero))));
        Workspace workspace = null!;

        // Act
        Action act = () => _ = builder.Build(
            workspace,
            new RepoSlug("repo"));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Build uses pull request list field group and created-on query")]
    [Trait("Category", "Unit")]
    public void BuildWhenCreatedOnModeConfiguredBuildsExpectedPath()
    {
        // Arrange
        var uriBuilder = new Mock<IBitbucketUriBuilder>(MockBehavior.Strict);
        var expectedPath =
            "repositories/ws/repo/pullrequests?pagelen=25&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on" +
            "&q=created_on%20%3E%3D%202026-02-15T00%3A00%3A00%2B00%3A00%20AND%20created_on%20%3C%202026-04-01T00%3A00%3A00%2B00%3A00";
        uriBuilder.Setup(x => x.BuildRelativeUri(
                It.Is<string>(path => path == expectedPath),
                It.Is<BitbucketFieldGroup>(group => group == BitbucketFieldGroup.PullRequestList)))
            .Returns(new Uri(expectedPath, UriKind.Relative));

        var builder = new PullRequestsUriBuilder(uriBuilder.Object, Options.Create(CreateOptions(
            filterDate: new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero),
            toDateExclusive: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero))));

        // Act
        var result = builder.Build(new Workspace("ws"), new RepoSlug("repo"));

        // Assert
        result.ToString().Should().Be(expectedPath);
        uriBuilder.VerifyAll();
    }

    [Fact(DisplayName = "Build adds updated-on and branch filters to query")]
    [Trait("Category", "Unit")]
    public void BuildWhenHistoricalModeAndMultipleBranchesConfiguredBuildsExpectedPath()
    {
        // Arrange
        var uriBuilder = new Mock<IBitbucketUriBuilder>(MockBehavior.Strict);
        var expectedPath =
            "repositories/ws/repo/pullrequests?pagelen=50&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on" +
            "&q=%28created_on%20%3E%3D%202026-02-15T00%3A00%3A00%2B00%3A00%20OR%20updated_on%20%3E%3D%202026-02-15T00%3A00%3A00%2B00%3A00%29%20AND%20%28destination.branch.name%20%3D%20%22develop%22%20OR%20destination.branch.name%20%3D%20%22master%22%29%20AND%20created_on%20%3C%202026-04-01T00%3A00%3A00%2B00%3A00";
        uriBuilder.Setup(x => x.BuildRelativeUri(
                It.Is<string>(path => path == expectedPath),
                It.Is<BitbucketFieldGroup>(group => group == BitbucketFieldGroup.PullRequestList)))
            .Returns(new Uri(expectedPath, UriKind.Relative));

        var builder = new PullRequestsUriBuilder(uriBuilder.Object, Options.Create(CreateOptions(
            filterDate: new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero),
            prTimeFilterMode: PrTimeFilterMode.LastKnownUpdateAndCreated,
            branchNameList: [new BranchName("develop"), new BranchName("master")],
            toDateExclusive: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            pageLength: 50)));

        // Act
        var result = builder.Build(new Workspace("ws"), new RepoSlug("repo"));

        // Assert
        result.ToString().Should().Be(expectedPath);
        uriBuilder.VerifyAll();
    }

    [Fact(DisplayName = "Build escapes branch names in query string literals")]
    [Trait("Category", "Unit")]
    public void BuildWhenBranchNameContainsSpecialCharactersEscapesLiteral()
    {
        // Arrange
        var uriBuilder = new Mock<IBitbucketUriBuilder>(MockBehavior.Strict);
        var expectedPath =
            "repositories/ws/repo/pullrequests?pagelen=25&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on" +
            "&q=created_on%20%3E%3D%202026-02-15T00%3A00%3A00%2B00%3A00%20AND%20destination.branch.name%20%3D%20%22release%5C%22candidate%5C%5C1%22%20AND%20created_on%20%3C%202026-02-16T00%3A00%3A00%2B00%3A00";
        uriBuilder.Setup(x => x.BuildRelativeUri(
                It.Is<string>(path => path == expectedPath),
                It.Is<BitbucketFieldGroup>(group => group == BitbucketFieldGroup.PullRequestList)))
            .Returns(new Uri(expectedPath, UriKind.Relative));

        var builder = new PullRequestsUriBuilder(uriBuilder.Object, Options.Create(CreateOptions(
            filterDate: new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero),
            branchNameList: [new BranchName("release\"candidate\\1")])));

        // Act
        var result = builder.Build(new Workspace("ws"), new RepoSlug("repo"));

        // Assert
        result.ToString().Should().Be(expectedPath);
        uriBuilder.VerifyAll();
    }

    private static BitbucketOptions CreateOptions(
        DateTimeOffset filterDate,
        PrTimeFilterMode prTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
        IReadOnlyList<BranchName>? branchNameList = null,
        DateTimeOffset? toDateExclusive = null,
        int pageLength = 25)
    {
        return new BitbucketOptions
        {
            Days = null,
            Workspace = "ws",
            FromDate = filterDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = (toDateExclusive ?? filterDate.AddDays(1)).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PageLength = pageLength,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = branchNameList?.Select(static branch => branch.Value).ToArray() ?? [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = prTimeFilterMode,
            Pdf = new PdfOptions()
        };
    }
}

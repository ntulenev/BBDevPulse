using System.Runtime.CompilerServices;

using FluentAssertions;

using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreRepositoryListPresenterTests
{
    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "FetchRepositoriesAsync throws when fetch callback is null")]
    [Trait("Category", "Unit")]
    public async Task FetchRepositoriesAsyncWhenFetchCallbackIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();
        Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetchRepositories = null!;

        // Act
        Func<Task> act = async () => _ = await presenter.FetchRepositoriesAsync(fetchRepositories, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "FetchRepositoriesAsync returns repositories from async stream and reports pages")]
    [Trait("Category", "Unit")]
    public async Task FetchRepositoriesAsyncWhenFetchCallbackReturnsValuesCollectsAllRepositories()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();

        // Act
        IReadOnlyList<Repository> repositories = [];
        var output = await TestConsoleRunner.RunAsync(async _ =>
        {
            repositories = await presenter.FetchRepositoriesAsync(FetchRepositoriesAsync, cancellationToken);
        });

        // Assert
        repositories.Should().HaveCount(2);
        repositories.Select(repo => repo.DisplayName).Should().Equal("RepoB", "RepoA");
        output.Should().Contain("Loading repositories: page 1");
        output.Should().Contain("Loading repositories: page 2");
    }

    [Fact(DisplayName = "FetchRepositoriesAsync propagates cancellation from repository stream")]
    [Trait("Category", "Unit")]
    public async Task FetchRepositoriesAsyncWhenStreamIsCanceledThrowsOperationCanceledException()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () =>
            _ = await presenter.FetchRepositoriesAsync(
                (_, token) => CanceledRepositoryStream(token),
                cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "RenderRepositoryTable throws when repositories are null")]
    [Trait("Category", "Unit")]
    public void RenderRepositoryTableWhenRepositoriesAreNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();
        IReadOnlyCollection<Repository> repositories = null!;

        // Act
        Action act = () => presenter.RenderRepositoryTable(
            repositories,
            RepoSearchMode.FilterFromTheList,
            new RepoNameFilter(string.Empty),
            []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderRepositoryTable throws when filter is null")]
    [Trait("Category", "Unit")]
    public void RenderRepositoryTableWhenFilterIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();
        RepoNameFilter filter = null!;

        // Act
        Action act = () => presenter.RenderRepositoryTable(
            [CreateRepository("RepoA", "repoa")],
            RepoSearchMode.FilterFromTheList,
            filter,
            []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderRepositoryTable throws when repository list filter is null")]
    [Trait("Category", "Unit")]
    public void RenderRepositoryTableWhenRepoListIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();
        IReadOnlyList<RepoName> repoList = null!;

        // Act
        Action act = () => presenter.RenderRepositoryTable(
            [CreateRepository("RepoA", "repoa")],
            RepoSearchMode.FilterFromTheList,
            new RepoNameFilter(string.Empty),
            repoList);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderRepositoryTable writes list title when using explicit list mode")]
    [Trait("Category", "Unit")]
    public void RenderRepositoryTableWhenListModeWithEntriesUsesListTitle()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderRepositoryTable(
            [CreateRepository("RepoB", "repob"), CreateRepository("RepoA", "repoa")],
            RepoSearchMode.FilterFromTheList,
            new RepoNameFilter(string.Empty),
            [new RepoName("RepoA"), new RepoName("RepoB")]));

        // Assert
        output.Should().Contain("Repositories (list count: 2)");
        output.IndexOf("RepoA", StringComparison.Ordinal).Should().BeLessThan(output.IndexOf("RepoB", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "RenderRepositoryTable writes contains title when using search-by-filter mode with non-empty filter")]
    [Trait("Category", "Unit")]
    public void RenderRepositoryTableWhenSearchByFilterWithTextUsesContainsTitle()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderRepositoryTable(
            [CreateRepository("RepoA", "repoa")],
            RepoSearchMode.SearchByFilter,
            new RepoNameFilter("pulse"),
            []));

        // Assert
        output.Should().Contain("Repositories (contains: pulse)");
    }

    [Fact(DisplayName = "RenderRepositoryTable writes all title for default cases")]
    [Trait("Category", "Unit")]
    public void RenderRepositoryTableWhenNoSpecificFilterTitleAppliesUsesAllTitle()
    {
        // Arrange
        var presenter = new SpectreRepositoryListPresenter();

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderRepositoryTable(
            [CreateRepository("RepoA", "repoa")],
            RepoSearchMode.FilterFromTheList,
            new RepoNameFilter(string.Empty),
            []));

        // Assert
        output.Should().Contain("Repositories (all)");
    }

    private static async IAsyncEnumerable<Repository> FetchRepositoriesAsync(
        Action<int> onPage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        onPage(1);
        yield return CreateRepository("RepoB", "repob");

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        onPage(2);
        yield return CreateRepository("RepoA", "repoa");
    }

    private static Repository CreateRepository(string name, string slug)
    {
        return new Repository(new RepoName(name), new RepoSlug(slug));
    }

    private static async IAsyncEnumerable<Repository> CanceledRepositoryStream(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return CreateRepository("Repo", "repo");
        await Task.Yield();
    }
}

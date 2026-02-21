using FluentAssertions;

using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreRepositoryAnalysisPresenterTests
{
    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "AnalyzeRepositoriesAsync throws when repositories list is null")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeRepositoriesAsyncWhenRepositoriesAreNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SpectreRepositoryAnalysisPresenter();
        IReadOnlyList<Repository> repositories = null!;

        // Act
        Func<Task> act = async () => await presenter.AnalyzeRepositoriesAsync(
            repositories,
            (_, _) => Task.CompletedTask,
            cancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnalyzeRepositoriesAsync throws when analyze callback is null")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeRepositoriesAsyncWhenAnalyzeRepositoryIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SpectreRepositoryAnalysisPresenter();
        Func<Repository, CancellationToken, Task> analyzeRepository = null!;

        // Act
        Func<Task> act = async () => await presenter.AnalyzeRepositoriesAsync(
            [CreateRepository("RepoA")],
            analyzeRepository,
            cancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnalyzeRepositoriesAsync invokes callback for each repository")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeRepositoriesAsyncWhenRepositoriesAreProvidedInvokesCallbackForEachRepository()
    {
        // Arrange
        var presenter = new SpectreRepositoryAnalysisPresenter();
        var repositories = new List<Repository>
        {
            CreateRepository("RepoA"),
            CreateRepository("RepoB")
        };
        var processedRepositoryNames = new List<string>();

        // Act
        _ = await TestConsoleRunner.RunAsync(async _ =>
        {
            await presenter.AnalyzeRepositoriesAsync(
                repositories,
                (repo, _) =>
                {
                    processedRepositoryNames.Add(repo.DisplayName);
                    return Task.CompletedTask;
                },
                cancellationToken);
        });

        // Assert
        processedRepositoryNames.Should().Equal("RepoA", "RepoB");
    }

    private static Repository CreateRepository(string name)
    {
        return new Repository(
            new RepoName(name),
            new RepoSlug(name.ToLowerInvariant()));
    }
}

using FluentAssertions;

using BBDevPulse.Configuration;
using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreRepositoryAnalysisPresenterTests
{
    [Fact]
    public async Task AnalyzeRepositoriesAsyncStopsSchedulingAfterHandlerFailure()
    {
        var presenter = CreatePresenter();
        var failure = new InvalidOperationException("repository failed");
        var processed = 0;

        var run = TestConsoleRunner.RunAsync(async _ => await presenter.AnalyzeRepositoriesAsync(
            [CreateRepository("RepoA"), CreateRepository("RepoB")],
            (_, _) =>
            {
                Interlocked.Increment(ref processed);
                return Task.FromException(failure);
            }, CancellationToken.None));

        var thrown = await FluentActions.Invoking(async () => await run)
            .Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(failure);
        processed.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeRepositoriesAsyncWaitsForActiveHandlerOnCancellation()
    {
        var presenter = CreatePresenter();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var processed = 0;

        var run = TestConsoleRunner.RunAsync(async _ => await presenter.AnalyzeRepositoriesAsync(
            [CreateRepository("RepoA"), CreateRepository("RepoB")],
            async (_, token) =>
            {
                Interlocked.Increment(ref active);
                Interlocked.Increment(ref processed);
                started.SetResult();
                try
                {
                    await finish.Task;
                    token.ThrowIfCancellationRequested();
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }, cancellation.Token));

        await started.Task;
        cancellation.Cancel();
        run.IsCompleted.Should().BeFalse();
        finish.SetResult();
        await FluentActions.Invoking(async () => await run).Should().ThrowAsync<OperationCanceledException>();
        active.Should().Be(0);
        processed.Should().Be(1);
    }

    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new SpectreRepositoryAnalysisPresenter(options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnalyzeRepositoriesAsync throws when repositories list is null")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeRepositoriesAsyncWhenRepositoriesAreNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = CreatePresenter();
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
        var presenter = CreatePresenter();
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
        var presenter = CreatePresenter();
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

    private static SpectreRepositoryAnalysisPresenter CreatePresenter(int repositoryConcurrency = 1) =>
        new(CreateOptions(repositoryConcurrency));

    private static IOptions<BitbucketOptions> CreateOptions(int repositoryConcurrency = 1) =>
        Options.Create(new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 50,
            PullRequestConcurrency = 1,
            RepositoryConcurrency = repositoryConcurrency,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Pdf = new PdfOptions()
        });
}

using BBDevPulse.Logic;
using BBDevPulse.Models;

using FluentAssertions;

namespace BBDevPulse.Tests.Logic;

public sealed class RepositoryAnalysisCoordinatorTests
{
    [Fact]
    public async Task HandlerFailureStopsSchedulingAndPreservesOriginalException()
    {
        var coordinator = new RepositoryAnalysisCoordinator(1);
        var failure = new InvalidOperationException("repository failed");
        var processed = 0;
        var repositories = new[]
        {
            new Repository(new RepoName("A"), new RepoSlug("a")),
            new Repository(new RepoName("B"), new RepoSlug("b"))
        };
        Func<Task> run = () => coordinator.RunAsync(repositories, (_, _) =>
        {
            Interlocked.Increment(ref processed);
            return Task.FromException(failure);
        }, _ => { }, CancellationToken.None);

        var thrown = await run.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(failure);
        processed.Should().Be(1);
    }

    [Fact]
    public async Task CancellationWaitsForActiveHandlerAndDoesNotStartMore()
    {
        var coordinator = new RepositoryAnalysisCoordinator(1);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = 0;
        var repositories = new[]
        {
            new Repository(new RepoName("A"), new RepoSlug("a")),
            new Repository(new RepoName("B"), new RepoSlug("b"))
        };
        var run = coordinator.RunAsync(repositories, async (_, token) =>
        {
            Interlocked.Increment(ref processed);
            started.SetResult();
            await release.Task;
            token.ThrowIfCancellationRequested();
        }, _ => { }, cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        run.IsCompleted.Should().BeFalse();
        release.SetResult();
        await FluentActions.Invoking(async () => await run).Should().ThrowAsync<OperationCanceledException>();
        processed.Should().Be(1);
    }

    [Fact]
    public async Task ExternalCancellationIsPreservedWhenLastHandlerCompletesNormally()
    {
        var coordinator = new RepositoryAnalysisCoordinator(1);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository(new RepoName("A"), new RepoSlug("a"));
        var run = coordinator.RunAsync([repository], async (_, _) =>
        {
            started.SetResult();
            await release.Task;
        }, _ => { }, cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        release.SetResult();
        await FluentActions.Invoking(async () => await run).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LimitsConcurrentRepositoriesWithoutPresentation()
    {
        var coordinator = new RepositoryAnalysisCoordinator(2);
        var active = 0;
        var maxActive = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repositories = Enumerable.Range(1, 4)
            .Select(i => new Repository(new RepoName($"Repo {i}"), new RepoSlug($"repo-{i}"))).ToArray();
        var run = coordinator.RunAsync(repositories, async (_, _) =>
        {
            var current = Interlocked.Increment(ref active);
            lock (repositories)
            {
                maxActive = System.Math.Max(maxActive, current);
            }
            if (current == 2)
            {
                bothStarted.TrySetResult();
            }
            await release.Task;
            Interlocked.Decrement(ref active);
        }, _ => { }, CancellationToken.None);

        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        maxActive.Should().Be(2);
        release.SetResult();
        await run;
        active.Should().Be(0);
        maxActive.Should().Be(2);
    }

    [Fact]
    public async Task ObserverFailureCancelsAndWaitsForStartedWork()
    {
        var coordinator = new RepositoryAnalysisCoordinator(2);
        var repositoryA = new Repository(new RepoName("A"), new RepoSlug("a"));
        var repositoryB = new Repository(new RepoName("B"), new RepoSlug("b"));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observerFailure = new InvalidOperationException("progress failed");
        var running = 0;
        var run = coordinator.RunAsync([repositoryA, repositoryB], async (_, token) =>
        {
            Interlocked.Increment(ref running);
            started.TrySetResult();
            await release.Task;
            Interlocked.Decrement(ref running);
            token.ThrowIfCancellationRequested();
        }, progress =>
        {
            if (progress.RepositoryName == "B")
            {
                throw observerFailure;
            }
        }, CancellationToken.None);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        run.IsCompleted.Should().BeFalse();
        release.SetResult();
        var thrown = await FluentActions.Invoking(async () => await run).Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(observerFailure);
        running.Should().Be(0);
    }
}

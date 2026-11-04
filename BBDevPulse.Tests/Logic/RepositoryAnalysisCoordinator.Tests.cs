using BBDevPulse.Logic;
using BBDevPulse.Models;

using FluentAssertions;

namespace BBDevPulse.Tests.Logic;

public sealed class RepositoryAnalysisCoordinatorTests
{
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

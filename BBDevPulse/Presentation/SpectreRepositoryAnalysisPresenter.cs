using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;
using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console repository analysis presenter.
/// </summary>
public sealed class SpectreRepositoryAnalysisPresenter : IRepositoryAnalysisPresenter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreRepositoryAnalysisPresenter"/> class.
    /// </summary>
    /// <param name="options">Bitbucket options.</param>
    public SpectreRepositoryAnalysisPresenter(IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _maxConcurrentRepositories = options.Value.RepositoryConcurrency;
    }

    /// <inheritdoc />
    public async Task AnalyzeRepositoriesAsync(
        IReadOnlyList<Repository> repositories,
        Func<Repository, CancellationToken, Task> analyzeRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(analyzeRepository);
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
                new ElapsedTimeColumn(),
                new RemainingTimeColumn(),
                new TaskDescriptionColumn())
            .StartAsync(async context =>
            {
                var task = context.AddTask("Analyzing repositories", maxValue: repositories.Count);
                var total = repositories.Count;
                var completed = 0;
                var inFlight = 0;
                var uiLock = new object();
                using var semaphore = new SemaphoreSlim(_maxConcurrentRepositories, _maxConcurrentRepositories);
                var repositoryTasks = new List<Task>(total);

                foreach (var repository in repositories)
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var running = Interlocked.Increment(ref inFlight);

                    lock (uiLock)
                    {
                        task.Description =
                            $"Analyzing {repository.DisplayName} ({Volatile.Read(ref completed)}/{total} complete, {running} active)";
                    }

                    repositoryTasks.Add(AnalyzeRepositoryAsync());

                    async Task AnalyzeRepositoryAsync()
                    {
                        try
                        {
                            await analyzeRepository(repository, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            var finished = Interlocked.Increment(ref completed);
                            var active = Interlocked.Decrement(ref inFlight);

                            lock (uiLock)
                            {
                                task.Increment(1);
                                task.Description = $"Analyzing repositories ({finished}/{total} complete, {active} active)";
                            }

                            _ = semaphore.Release();
                        }
                    }
                }

                await Task.WhenAll(repositoryTasks).ConfigureAwait(false);

                lock (uiLock)
                {
                    task.Description = $"Analyzing repositories ({completed}/{total} complete)";
                }
            })
            .ConfigureAwait(false);
    }

    private readonly int _maxConcurrentRepositories;
}

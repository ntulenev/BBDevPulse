using BBDevPulse.Abstractions;
using System.Runtime.ExceptionServices;
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
                using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var repositoryTasks = new List<Task>(total);
                Exception? sourceError = null;
                Exception? handlerError = null;
                try
                {
                    foreach (var repository in repositories)
                    {
                        await semaphore.WaitAsync(stop.Token).ConfigureAwait(false);
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
                                await analyzeRepository(repository, stop.Token).ConfigureAwait(false);
                            }
                            catch (Exception exception) when (!stop.IsCancellationRequested)
                            {
                                Interlocked.CompareExchange(ref handlerError, exception, null);
                                stop.Cancel();
                                throw;
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
                }
                catch (Exception exception)
                {
                    sourceError = exception is OperationCanceledException &&
                        !cancellationToken.IsCancellationRequested && handlerError is not null
                        ? handlerError : exception;
                    await stop.CancelAsync().ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        await Task.WhenAll(repositoryTasks).ConfigureAwait(false);
                    }
                    catch when (sourceError is not null)
                    {
                    }
                }
                if (sourceError is not null)
                {
                    ExceptionDispatchInfo.Capture(sourceError).Throw();
                }

                lock (uiLock)
                {
                    task.Description = $"Analyzing repositories ({completed}/{total} complete)";
                }
            })
            .ConfigureAwait(false);
    }

    private readonly int _maxConcurrentRepositories;
}

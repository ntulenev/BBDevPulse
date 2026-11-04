using System.Runtime.ExceptionServices;

using BBDevPulse.Models;

namespace BBDevPulse.Logic;

internal sealed record RepositoryAnalysisProgress(string? RepositoryName, int Completed, int Active, int Total);

// Owns repository scheduling and drains all started work before returning.
internal sealed class RepositoryAnalysisCoordinator(int concurrency)
{
    public async Task RunAsync(IReadOnlyList<Repository> repositories,
        Func<Repository, CancellationToken, Task> analyze, Action<RepositoryAnalysisProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(analyze);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrency);
        using var semaphore = new SemaphoreSlim(concurrency, concurrency);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = new List<Task>(repositories.Count);
        var sync = new object();
        var completed = 0;
        var active = 0;
        Exception? firstError = null;
        Exception? sourceError = null;

        void RecordError(Exception error)
        {
            Interlocked.CompareExchange(ref firstError, error, null);
            stop.Cancel();
        }

        void Notify(string? repository, bool start)
        {
            lock (sync)
            {
                if (start)
                {
                    active++;
                }
                else
                {
                    completed++;
                    active--;
                }
                progress(new RepositoryAnalysisProgress(repository, completed, active, repositories.Count));
            }
        }

        async Task Execute(Repository repository)
        {
            try
            {
                await analyze(repository, stop.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (!stop.IsCancellationRequested)
            {
                RecordError(exception);
                throw;
            }
            finally
            {
                try
                {
                    Notify(null, false);
                }
                catch (Exception exception)
                {
                    RecordError(exception);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        try
        {
            foreach (var repository in repositories)
            {
                await semaphore.WaitAsync(stop.Token).ConfigureAwait(false);
                try
                {
                    Notify(repository.DisplayName, true);
                }
                catch
                {
                    semaphore.Release();
                    throw;
                }
                tasks.Add(Execute(repository));
            }
        }
        catch (Exception exception)
        {
            sourceError = exception is OperationCanceledException &&
                !cancellationToken.IsCancellationRequested && firstError is not null ? firstError : exception;
            await stop.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                sourceError ??= firstError ?? exception;
            }
        }
        sourceError ??= firstError;
        if (sourceError is not null)
        {
            ExceptionDispatchInfo.Capture(sourceError).Throw();
        }
    }
}

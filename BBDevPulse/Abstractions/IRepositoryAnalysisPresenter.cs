using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Presents repository analysis progress.
/// </summary>
public interface IRepositoryAnalysisPresenter
{
    /// <summary>Displays progress while the application runs repository analysis.</summary>
    /// <param name="total">Number of repositories.</param>
    /// <param name="run">Application operation receiving a progress callback and cancellation token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RunWithRepositoryProgressAsync(int total,
        Func<Action<RepositoryAnalysisProgress>, CancellationToken, Task> run,
        CancellationToken cancellationToken);

}

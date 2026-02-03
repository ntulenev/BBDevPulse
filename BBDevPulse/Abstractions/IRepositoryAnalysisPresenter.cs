using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Presents repository analysis progress.
/// </summary>
public interface IRepositoryAnalysisPresenter
{
    /// <summary>
    /// Runs the repository analysis with progress UI.
    /// </summary>
    /// <param name="repositories">Repositories to analyze.</param>
    /// <param name="analyzeRepository">Callback invoked for each repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AnalyzeRepositoriesAsync(
        IReadOnlyList<Repository> repositories,
        Func<Repository, CancellationToken, Task> analyzeRepository,
        CancellationToken cancellationToken);
}

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console repository analysis presenter.
/// </summary>
public sealed class SpectreRepositoryAnalysisPresenter : IRepositoryAnalysisPresenter
{
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

                foreach (var repo in repositories)
                {
                    task.Description = $"Analyzing {repo.DisplayName}";
                    await analyzeRepository(repo, cancellationToken).ConfigureAwait(false);
                    task.Increment(1);
                }
            })
            .ConfigureAwait(false);
    }
}

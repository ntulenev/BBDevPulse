using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Logic;
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
                var coordinator = new RepositoryAnalysisCoordinator(_maxConcurrentRepositories);
                await coordinator.RunAsync(repositories, analyzeRepository, update =>
                {
                    task.Value = update.Completed;
                    task.Description = update.RepositoryName is not null
                        ? $"Analyzing {update.RepositoryName} ({update.Completed}/{update.Total} complete, {update.Active} active)"
                        : $"Analyzing repositories ({update.Completed}/{update.Total} complete, {update.Active} active)";
                }, cancellationToken).ConfigureAwait(false);
                task.Description = $"Analyzing repositories ({repositories.Count}/{repositories.Count} complete)";
            })
            .ConfigureAwait(false);
    }

    private readonly int _maxConcurrentRepositories;
}

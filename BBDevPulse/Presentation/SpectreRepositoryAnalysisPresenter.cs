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
    public async Task RunWithRepositoryProgressAsync(int total,
        Func<Action<RepositoryAnalysisProgress>, CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentOutOfRangeException.ThrowIfNegative(total);
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
                var task = context.AddTask("Analyzing repositories", maxValue: total);
                await run(update =>
                {
                    task.Value = update.Completed;
                    task.Description = update.RepositoryName is not null
                        ? $"Analyzing {update.RepositoryName} ({update.Completed}/{update.Total} complete, {update.Active} active)"
                        : $"Analyzing repositories ({update.Completed}/{update.Total} complete, {update.Active} active)";
                }, cancellationToken).ConfigureAwait(false);
                task.Description = $"Analyzing repositories ({total}/{total} complete)";
            }).ConfigureAwait(false);
    }

}

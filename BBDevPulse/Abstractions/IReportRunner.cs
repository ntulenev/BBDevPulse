namespace BBDevPulse.Abstractions;

/// <summary>
/// Orchestrates the end-to-end report execution.
/// </summary>
public interface IReportRunner
{
    /// <summary>
    /// Runs the report workflow.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RunAsync(CancellationToken cancellationToken);
}

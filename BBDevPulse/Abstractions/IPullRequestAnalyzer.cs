using BBDevPulse.Models;
using BBDevPulse.Logic;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Analyzes pull requests and produces reporting data.
/// </summary>
internal interface IPullRequestAnalyzer
{
    /// <summary>
    /// Analyzes pull requests for a repository and updates report data.
    /// </summary>
    /// <param name="repo">Repository to analyze.</param>
    /// <param name="reportData">Report output data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AnalyzeAsync(
        Repository repo,
        ReportAccumulator reportData,
        CancellationToken cancellationToken);
}

using BBDevPulse.Configuration;
using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Analyzes pull requests and produces reporting data.
/// </summary>
public interface IPullRequestAnalyzer
{
    /// <summary>
    /// Analyzes pull requests for a repository and updates report data.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <param name="repo">Repository to analyze.</param>
    /// <param name="filterDate">Filter cutoff date.</param>
    /// <param name="prTimeFilterMode">Pull request time filter mode.</param>
    /// <param name="branchNameList">Branch filter list.</param>
    /// <param name="reports">Report output collection.</param>
    /// <param name="developerStats">Developer statistics output collection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AnalyzeAsync(
        Workspace workspace,
        Repository repo,
        DateTimeOffset filterDate,
        PrTimeFilterMode prTimeFilterMode,
        IReadOnlyList<BranchName> branchNameList,
        List<PullRequestReport> reports,
        Dictionary<DeveloperKey, DeveloperStats> developerStats,
        CancellationToken cancellationToken);
}

using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Presents pull request reports.
/// </summary>
public interface IPullRequestReportPresenter
{
    /// <summary>
    /// Renders the pull request table.
    /// </summary>
    /// <param name="reports">Pull request reports.</param>
    /// <param name="filterDate">Filter cutoff date.</param>
    void RenderPullRequestTable(
        IReadOnlyCollection<PullRequestReport> reports,
        DateTimeOffset filterDate);
}

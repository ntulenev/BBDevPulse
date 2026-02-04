
namespace BBDevPulse.Models;

/// <summary>
/// Aggregates report output collections.
/// </summary>
public sealed class ReportData
{
    /// <summary>
    /// Gets the pull request reports.
    /// </summary>
    public List<PullRequestReport> Reports { get; } = [];

    /// <summary>
    /// Gets the developer statistics keyed by identity.
    /// </summary>
    public Dictionary<DeveloperKey, DeveloperStats> DeveloperStats { get; } = [];
}

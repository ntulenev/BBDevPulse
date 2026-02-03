using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Analyzes pull request activities and updates report data.
/// </summary>
internal interface IActivityAnalyzer
{
    /// <summary>
    /// Processes pull request activities and updates analysis state.
    /// </summary>
    /// <param name="analysis">Current analysis state.</param>
    /// <param name="activity">Activity entry to analyze.</param>
    /// <param name="filterDate">Filter cutoff date.</param>
    void Analyze(ActivityAnalysisState analysis, PullRequestActivity activity, DateTimeOffset filterDate);
}

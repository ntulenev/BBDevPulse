namespace BBDevPulse.Models;

/// <summary>
/// Aggregated pull request size details.
/// </summary>
public readonly record struct PullRequestSizeSummary(
    int FilesChanged,
    int LinesAdded,
    int LinesRemoved)
{
    /// <summary>
    /// Empty size summary.
    /// </summary>
    public static PullRequestSizeSummary Empty => new(0, 0, 0);

    /// <summary>
    /// Total changed lines (added + removed).
    /// </summary>
    public int LineChurn => LinesAdded + LinesRemoved;

    /// <summary>
    /// Net changed lines (added - removed).
    /// </summary>
    public int NetLines => LinesAdded - LinesRemoved;
}

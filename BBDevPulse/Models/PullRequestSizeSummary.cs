namespace BBDevPulse.Models;

/// <summary>
/// Aggregated pull request size details.
/// </summary>
public readonly record struct PullRequestSizeSummary
{
    /// <summary>Creates a size measurement whose total line count fits into an integer.</summary>
    public PullRequestSizeSummary(int FilesChanged, int LinesAdded, int LinesRemoved)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(FilesChanged);
        ArgumentOutOfRangeException.ThrowIfNegative(LinesAdded);
        ArgumentOutOfRangeException.ThrowIfNegative(LinesRemoved);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(LinesAdded, int.MaxValue - LinesRemoved);
        this.FilesChanged = FilesChanged;
        this.LinesAdded = LinesAdded;
        this.LinesRemoved = LinesRemoved;
    }

    /// <summary>Number of files in the diff.</summary>
    public int FilesChanged { get; }

    /// <summary>Number of lines added.</summary>
    public int LinesAdded { get; }

    /// <summary>Number of lines removed.</summary>
    public int LinesRemoved { get; }

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

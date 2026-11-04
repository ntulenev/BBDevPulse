namespace BBDevPulse.Models;

/// <summary>
/// Aggregated pull request size details.
/// </summary>
public readonly record struct PullRequestSizeSummary
{
    /// <summary>Creates a size measurement whose total line count fits into an integer.</summary>
    public PullRequestSizeSummary(int FilesChanged, int LinesAdded, int LinesRemoved, bool hasLineData = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(FilesChanged);
        ArgumentOutOfRangeException.ThrowIfNegative(LinesAdded);
        ArgumentOutOfRangeException.ThrowIfNegative(LinesRemoved);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(LinesAdded, int.MaxValue - LinesRemoved);
        this.FilesChanged = FilesChanged;
        this.LinesAdded = LinesAdded;
        this.LinesRemoved = LinesRemoved;
        HasLineData = hasLineData;
    }

    /// <summary>Number of files in the diff.</summary>
    public int FilesChanged { get; }

    /// <summary>Known added lines; partial when <see cref="HasLineData"/> is false.</summary>
    public int LinesAdded { get; }

    /// <summary>Known removed lines; partial when <see cref="HasLineData"/> is false.</summary>
    public int LinesRemoved { get; }

    /// <summary>Whether all file entries contained both line counts.</summary>
    public bool HasLineData { get; }

    /// <summary>
    /// Empty size summary.
    /// </summary>
    public static PullRequestSizeSummary Empty => new(0, 0, 0);

    /// <summary>
    /// Total changed lines (added + removed).
    /// </summary>
    public int LineChurn => HasLineData ? LinesAdded + LinesRemoved :
        throw new InvalidOperationException("Line counts are incomplete.");

    /// <summary>
    /// Net changed lines (added - removed).
    /// </summary>
    public int NetLines => HasLineData ? LinesAdded - LinesRemoved :
        throw new InvalidOperationException("Line counts are incomplete.");
}

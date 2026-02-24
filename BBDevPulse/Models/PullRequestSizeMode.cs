namespace BBDevPulse.Models;

/// <summary>
/// Controls how pull request size is measured.
/// </summary>
public enum PullRequestSizeMode
{
    /// <summary>
    /// Measure size by changed lines (added + removed).
    /// </summary>
    Lines = 1,

    /// <summary>
    /// Measure size by number of changed files.
    /// </summary>
    Files = 2
}

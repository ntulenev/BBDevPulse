namespace BBDevPulse.Models;

/// <summary>
/// Pull request state domain model.
/// </summary>
public enum PullRequestState
{
    /// <summary>
    /// Unknown state.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Open pull request.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Merged pull request.
    /// </summary>
    Merged = 2,

    /// <summary>
    /// Declined pull request.
    /// </summary>
    Declined = 3,

    /// <summary>
    /// Superseded pull request.
    /// </summary>
    Superseded = 4
}

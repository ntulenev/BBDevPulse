namespace BBDevPulse.Models;

/// <summary>
/// Controls how pull request time filtering is applied.
/// </summary>
public enum PrTimeFilterMode
{
    /// <summary>
    /// Evaluate both last update and creation timestamps.
    /// </summary>
    LastKnownUpdateAndCreated = 1,

    /// <summary>
    /// Evaluate only creation timestamp.
    /// </summary>
    CreatedOnOnly = 2
}

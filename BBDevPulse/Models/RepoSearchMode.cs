namespace BBDevPulse.Models;

/// <summary>
/// Controls repository selection behavior.
/// </summary>
public enum RepoSearchMode
{
    /// <summary>
    /// Filter repositories by substring match.
    /// </summary>
    SearchByFilter = 1,

    /// <summary>
    /// Filter repositories by explicit list.
    /// </summary>
    FilterFromTheList = 2
}

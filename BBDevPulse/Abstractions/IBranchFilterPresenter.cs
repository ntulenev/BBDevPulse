using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Presents branch filter information.
/// </summary>
public interface IBranchFilterPresenter
{
    /// <summary>
    /// Renders branch filter information.
    /// </summary>
    /// <param name="branchList">Branch names to filter by.</param>
    void RenderBranchFilterInfo(IReadOnlyList<BranchName> branchList);
}

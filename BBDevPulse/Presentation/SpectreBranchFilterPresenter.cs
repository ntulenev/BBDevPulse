using BBDevPulse.Abstractions;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console branch filter presenter.
/// </summary>
public sealed class SpectreBranchFilterPresenter : IBranchFilterPresenter
{
    /// <inheritdoc />
    public void RenderBranchFilterInfo(IReadOnlyList<BranchName> branchList)
    {
        ArgumentNullException.ThrowIfNull(branchList);
        if (branchList.Count == 0)
        {
            return;
        }

        var joined = string.Join(", ", branchList.Select(branch => branch.Value));
        AnsiConsole.MarkupLine($"[grey]Filtering PRs by target branches:[/] {joined}");
    }
}

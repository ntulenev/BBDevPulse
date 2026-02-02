namespace BBDevPulse.Models;

/// <summary>
/// Pull request destination domain model.
/// </summary>
public sealed class PullRequestDestination
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestDestination"/> class.
    /// </summary>
    /// <param name="branch">Destination branch.</param>
    public PullRequestDestination(PullRequestBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        Branch = branch;
    }

    /// <summary>
    /// Destination branch.
    /// </summary>
    public PullRequestBranch Branch { get; }
}

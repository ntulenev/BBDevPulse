namespace BBDevPulse.Models;

/// <summary>
/// Pull request branch domain model.
/// </summary>
public sealed class PullRequestBranch
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestBranch"/> class.
    /// </summary>
    /// <param name="name">Branch name.</param>
    public PullRequestBranch(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>
    /// Branch name.
    /// </summary>
    public string Name { get; }
}

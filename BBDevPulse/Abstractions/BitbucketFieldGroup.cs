namespace BBDevPulse.Abstractions;

/// <summary>
/// Predefined groups of Bitbucket API fields used for partial responses.
/// </summary>
internal enum BitbucketFieldGroup
{
    /// <summary>
    /// Do not append a fields filter.
    /// </summary>
    None = 0,

    /// <summary>
    /// Repository collection fields.
    /// </summary>
    RepositoryList = 1,

    /// <summary>
    /// Pull request collection fields.
    /// </summary>
    PullRequestList = 2,

    /// <summary>
    /// Pull request activity collection fields.
    /// </summary>
    PullRequestActivity = 3,

    /// <summary>
    /// Pull request commit collection fields.
    /// </summary>
    PullRequestCommit = 4,

    /// <summary>
    /// Pull request self endpoint fields used to resolve commit hashes.
    /// </summary>
    PullRequestSizeReference = 5,

    /// <summary>
    /// Diffstat collection fields.
    /// </summary>
    PullRequestDiffStat = 6
}

using System;
using System.Collections.Generic;

namespace BBDevPulse.Models;

/// <summary>
/// Aggregates report creation inputs derived from configuration.
/// </summary>
public sealed class ReportParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportParameters"/> class.
    /// </summary>
    /// <param name="filterDate">Filter date for pull requests.</param>
    /// <param name="workspace">Bitbucket workspace.</param>
    /// <param name="repoNameFilter">Repository name filter.</param>
    /// <param name="repoNameList">Repository list filter.</param>
    /// <param name="repoSearchMode">Repository search mode.</param>
    /// <param name="prTimeFilterMode">Pull request time filter mode.</param>
    /// <param name="branchNameList">Target branch filter list.</param>
    public ReportParameters(
        DateTimeOffset filterDate,
        Workspace workspace,
        RepoNameFilter repoNameFilter,
        IReadOnlyList<RepoName> repoNameList,
        RepoSearchMode repoSearchMode,
        PrTimeFilterMode prTimeFilterMode,
        IReadOnlyList<BranchName> branchNameList)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoNameFilter);
        ArgumentNullException.ThrowIfNull(repoNameList);
        ArgumentNullException.ThrowIfNull(branchNameList);

        FilterDate = filterDate;
        Workspace = workspace;
        RepoNameFilter = repoNameFilter;
        RepoNameList = repoNameList;
        RepoSearchMode = repoSearchMode;
        PrTimeFilterMode = prTimeFilterMode;
        BranchNameList = branchNameList;
    }

    /// <summary>
    /// Gets the pull request filter date.
    /// </summary>
    public DateTimeOffset FilterDate { get; }

    /// <summary>
    /// Gets the Bitbucket workspace.
    /// </summary>
    public Workspace Workspace { get; }

    /// <summary>
    /// Gets the repository name filter.
    /// </summary>
    public RepoNameFilter RepoNameFilter { get; }

    /// <summary>
    /// Gets the repository list filter.
    /// </summary>
    public IReadOnlyList<RepoName> RepoNameList { get; }

    /// <summary>
    /// Gets the repository search mode.
    /// </summary>
    public RepoSearchMode RepoSearchMode { get; }

    /// <summary>
    /// Gets the pull request time filter mode.
    /// </summary>
    public PrTimeFilterMode PrTimeFilterMode { get; }

    /// <summary>
    /// Gets the target branch filter list.
    /// </summary>
    public IReadOnlyList<BranchName> BranchNameList { get; }
}

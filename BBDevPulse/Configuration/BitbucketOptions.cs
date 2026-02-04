using System;
using System.Linq;

namespace BBDevPulse.Configuration;

/// <summary>
/// Bitbucket configuration settings bound from appsettings.json.
/// </summary>
internal sealed class BitbucketOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
    public const string SECTION_NAME = "Bitbucket";
#pragma warning restore CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Lookback window in days.
    /// </summary>
    public required int Days { get; init; }

    /// <summary>
    /// Bitbucket workspace key.
    /// </summary>
    public required string Workspace { get; init; }

    /// <summary>
    /// Page size for Bitbucket API requests.
    /// </summary>
    public required int PageLength { get; init; }

    /// <summary>
    /// Bitbucket username.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Bitbucket app password.
    /// </summary>
    public required string AppPassword { get; init; }

    /// <summary>
    /// Repository name filter.
    /// </summary>
    public required string RepoNameFilter { get; init; }

    /// <summary>
    /// Repository list filter.
    /// </summary>
    public required string[] RepoNameList { get; init; }

    /// <summary>
    /// Target branch filter list.
    /// </summary>
    public required string[] BranchNameList { get; init; }

    /// <summary>
    /// Repository search mode.
    /// </summary>
    public required Models.RepoSearchMode RepoSearchMode { get; init; }

    /// <summary>
    /// Pull request time filter mode.
    /// </summary>
    public required Models.PrTimeFilterMode PrTimeFilterMode { get; init; }

    /// <summary>
    /// Builds report parameters from the current options.
    /// </summary>
    public Models.ReportParameters CreateReportParameters()
    {
        var filterDate = DateTimeOffset.UtcNow.AddDays(-Days);
        var workspace = new Models.Workspace(Workspace);
        var repoNameFilter = new Models.RepoNameFilter(RepoNameFilter);
        var repoNameList = (RepoNameList ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => new Models.RepoName(entry))
            .ToList();
        var branchNameList = (BranchNameList ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => new Models.BranchName(entry))
            .ToList();

        return new Models.ReportParameters(
            filterDate,
            workspace,
            repoNameFilter,
            repoNameList,
            RepoSearchMode,
            PrTimeFilterMode,
            branchNameList);
    }
}

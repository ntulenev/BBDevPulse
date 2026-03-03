using System.Collections.Frozen;

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
    /// <param name="excludeWeekend">Whether to exclude weekends in duration calculations.</param>
    /// <param name="excludedDays">Optional list of excluded days.</param>
    /// <param name="pullRequestSizeMode">Pull request size mode.</param>
    /// <param name="teamFilter">Optional team filter resolved from people CSV.</param>
    /// <param name="showDeveloperUuidInStats">Whether to show Bitbucket UUIDs in developer stats.</param>
    /// <param name="toDateExclusive">Optional exclusive upper bound for the report range.</param>
    public ReportParameters(
        DateTimeOffset filterDate,
        Workspace workspace,
        RepoNameFilter repoNameFilter,
        IReadOnlyList<RepoName> repoNameList,
        RepoSearchMode repoSearchMode,
        PrTimeFilterMode prTimeFilterMode,
        IReadOnlyList<BranchName> branchNameList,
        bool excludeWeekend = false,
        IReadOnlyList<DateOnly>? excludedDays = null,
        PullRequestSizeMode pullRequestSizeMode = PullRequestSizeMode.Lines,
        string? teamFilter = null,
        bool showDeveloperUuidInStats = false,
        DateTimeOffset? toDateExclusive = null)
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
        ExcludeWeekend = excludeWeekend;
        PullRequestSizeMode = pullRequestSizeMode;
        TeamFilter = string.IsNullOrWhiteSpace(teamFilter)
            ? null
            : teamFilter.Trim();
        ShowDeveloperUuidInStats = showDeveloperUuidInStats;
        ToDateExclusive = toDateExclusive;
        ExcludedDays = excludedDays is null
            ? new HashSet<DateOnly>().ToFrozenSet()
            : new HashSet<DateOnly>(excludedDays).ToFrozenSet();
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

    /// <summary>
    /// Gets whether to exclude weekends in duration calculations.
    /// </summary>
    public bool ExcludeWeekend { get; }

    /// <summary>
    /// Gets pull request size mode.
    /// </summary>
    public PullRequestSizeMode PullRequestSizeMode { get; }

    /// <summary>
    /// Gets optional team filter resolved from people CSV.
    /// </summary>
    public string? TeamFilter { get; }

    /// <summary>
    /// Gets a value indicating whether a team filter is active.
    /// </summary>
    public bool HasTeamFilter => !string.IsNullOrWhiteSpace(TeamFilter);

    /// <summary>
    /// Gets a value indicating whether Bitbucket UUIDs should be shown in developer stats.
    /// </summary>
    public bool ShowDeveloperUuidInStats { get; }

    /// <summary>
    /// Gets optional exclusive upper bound for the report range.
    /// </summary>
    public DateTimeOffset? ToDateExclusive { get; }

    /// <summary>
    /// Gets a value indicating whether the report uses a bounded date range.
    /// </summary>
    public bool HasUpperBound => ToDateExclusive.HasValue;

    /// <summary>
    /// Determines whether the provided timestamp is inside the configured report range.
    /// </summary>
    /// <param name="timestamp">Timestamp to test.</param>
    /// <returns>True when the timestamp is in range.</returns>
    public bool IsInRange(DateTimeOffset timestamp) =>
        timestamp >= FilterDate &&
        (!ToDateExclusive.HasValue || timestamp < ToDateExclusive.Value);

    /// <summary>
    /// Determines whether the provided timestamp is inside the configured report range.
    /// </summary>
    /// <param name="timestamp">Timestamp to test.</param>
    /// <returns>True when the timestamp is in range.</returns>
    public bool IsInRange(DateTimeOffset? timestamp) =>
        timestamp.HasValue && IsInRange(timestamp.Value);

    /// <summary>
    /// Formats the report date window for display.
    /// </summary>
    /// <returns>Human readable date window.</returns>
    public string GetDateWindowLabel() =>
        ToDateExclusive.HasValue
            ? $"{FilterDate:yyyy-MM-dd} to {ToDateExclusive.Value.AddDays(-1):yyyy-MM-dd}"
            : $"since {FilterDate:yyyy-MM-dd}";

    /// <summary>
    /// Gets optional list of excluded days.
    /// </summary>
    public IReadOnlySet<DateOnly> ExcludedDays { get; }
}

using System.Globalization;

namespace BBDevPulse.Configuration;

/// <summary>
/// Bitbucket configuration settings bound from appsettings.json.
/// </summary>
public sealed class BitbucketOptions
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
    public int? Days { get; init; }

    /// <summary>
    /// Bitbucket workspace key.
    /// </summary>
    public required string Workspace { get; init; }

    /// <summary>
    /// Optional inclusive start date for report range in dd.MM.yyyy or yyyy-MM-dd format.
    /// </summary>
    public string? FromDate { get; init; }

    /// <summary>
    /// Optional inclusive end date for report range in dd.MM.yyyy or yyyy-MM-dd format.
    /// </summary>
    public string? ToDate { get; init; }

    /// <summary>
    /// Page size for Bitbucket API requests.
    /// </summary>
    public required int PageLength { get; init; }

    /// <summary>
    /// Maximum number of pull requests to analyze concurrently per repository.
    /// </summary>
    public int PullRequestConcurrency { get; init; } = 1;

    /// <summary>
    /// Maximum number of repositories to analyze concurrently.
    /// </summary>
    public int RepositoryConcurrency { get; init; } = 1;

    /// <summary>
    /// Maximum number of retries for retryable Bitbucket API requests.
    /// </summary>
    public int MaxRetries { get; init; } = 5;

    /// <summary>
    /// Step in seconds used to increase retry delays.
    /// </summary>
    public int RetryDelayStepSeconds { get; init; } = 1;

    /// <summary>
    /// Maximum retry delay in seconds for Bitbucket API request backoff.
    /// </summary>
    public int MaxRetryDelaySeconds { get; init; } = 30;

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
    /// Pull request size mode (lines or files).
    /// </summary>
    public Models.PullRequestSizeMode PullRequestSizeMode { get; init; } = Models.PullRequestSizeMode.Lines;

    /// <summary>
    /// Whether to exclude weekends from duration-based metrics.
    /// </summary>
    public bool ExcludeWeekend { get; init; }

    /// <summary>
    /// Optional list of excluded days in dd.MM.yyyy or yyyy-MM-dd format.
    /// </summary>
    public IReadOnlyList<string>? ExcludedDays { get; init; }

    /// <summary>
    /// Optional path to a CSV file with people metadata (Name;Grade;Department).
    /// </summary>
    public string? PeopleCsvPath { get; init; }

    /// <summary>
    /// Optional team name filter resolved from the people CSV.
    /// </summary>
    public string? TeamFilter { get; init; }

    /// <summary>
    /// Whether to show Bitbucket UUIDs in developer stats.
    /// </summary>
    public bool ShowDeveloperUuidInStats { get; init; }

    /// <summary>
    /// Whether to append detailed per-developer activity sections after the summary reports.
    /// </summary>
    public bool ShowAllDetailsForDevelopers { get; init; }

    /// <summary>
    /// HTML report output options.
    /// </summary>
    public HtmlOptions Html { get; init; } = new();

    /// <summary>
    /// PDF report output options.
    /// </summary>
    public PdfOptions Pdf { get; init; } = new();

    /// <summary>
    /// Builds report parameters from the current options.
    /// </summary>
    public Models.ReportParameters CreateReportParameters()
    {
        var (filterDate, toDateExclusive) = ResolveDateRange();
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
        DateOnly[] excludedDays = ExcludedDays is null
            ? []
            : [.. ExcludedDays
                .Where(static day => !string.IsNullOrWhiteSpace(day))
                .Select(static day => ParseExcludedDay(day.Trim()))
                .Distinct()];

        return new Models.ReportParameters(
            filterDate,
            workspace,
            repoNameFilter,
            repoNameList,
            RepoSearchMode,
            PrTimeFilterMode,
            branchNameList,
            ExcludeWeekend,
            excludedDays,
            PullRequestSizeMode,
            TeamFilter,
            ShowDeveloperUuidInStats,
            ShowAllDetailsForDevelopers,
            toDateExclusive);
    }

    private (DateTimeOffset FilterDate, DateTimeOffset? ToDateExclusive) ResolveDateRange()
    {
        if (!string.IsNullOrWhiteSpace(FromDate) || !string.IsNullOrWhiteSpace(ToDate))
        {
            if (string.IsNullOrWhiteSpace(FromDate) || string.IsNullOrWhiteSpace(ToDate))
            {
                throw new FormatException("Both FromDate and ToDate must be configured together.");
            }

            var fromDate = ParseExcludedDay(FromDate.Trim());
            var toDate = ParseExcludedDay(ToDate.Trim());
            if (toDate < fromDate)
            {
                throw new FormatException($"ToDate '{toDate:yyyy-MM-dd}' must be on or after FromDate '{fromDate:yyyy-MM-dd}'.");
            }

            return (
                new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        }

        if (!Days.HasValue || Days.Value <= 0)
        {
            throw new FormatException("Days must be greater than 0 when FromDate and ToDate are not configured.");
        }

        return (DateTimeOffset.UtcNow.AddDays(-Days.Value), null);
    }

    private static DateOnly ParseExcludedDay(string value)
    {
        if (DateOnly.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return day;
        }

        throw new FormatException($"Invalid excluded day '{value}'. Expected dd.MM.yyyy or yyyy-MM-dd.");
    }
}

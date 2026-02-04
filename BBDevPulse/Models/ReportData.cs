
namespace BBDevPulse.Models;

/// <summary>
/// Aggregates report output collections.
/// </summary>
public sealed class ReportData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportData"/> class.
    /// </summary>
    /// <param name="parameters">Report parameters.</param>
    public ReportData(ReportParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the report parameters.
    /// </summary>
    public ReportParameters Parameters { get; }

    /// <summary>
    /// Gets the pull request reports.
    /// </summary>
    public List<PullRequestReport> Reports { get; } = [];

    /// <summary>
    /// Gets the developer statistics keyed by identity.
    /// </summary>
    public Dictionary<DeveloperKey, DeveloperStats> DeveloperStats { get; } = [];

    /// <summary>
    /// Sorts reports by creation time, ascending.
    /// </summary>
    public void SortReportsByCreatedOn() => Reports.Sort((left, right) => left.CreatedOn.CompareTo(right.CreatedOn));

    public DeveloperStats GetOrAddDeveloper(DeveloperIdentity identity)
    {
        var key = DeveloperKey.FromIdentity(identity);
        if (DeveloperStats.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = new DeveloperStats(identity.DisplayName);
        DeveloperStats[key] = created;
        return created;
    }
}


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
    public ReportData(
        ReportParameters parameters,
        IReadOnlyDictionary<DisplayName, PersonCsvRow>? peopleByDisplayName = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Parameters = parameters;
        _peopleByDisplayName = peopleByDisplayName is null
            ? new Dictionary<string, PersonCsvRow>(StringComparer.Ordinal)
            : peopleByDisplayName.ToDictionary(
                static entry => entry.Key.Value,
                static entry => entry.Value,
                StringComparer.Ordinal);
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
    /// Gets the number of pull requests created inside the selected range.
    /// </summary>
    public int PullRequestsCreatedInRange =>
        Reports.Count(report => report.IncludeInMetrics && Parameters.IsInRange(report.CreatedOn));

    /// <summary>
    /// Gets the number of pull requests merged inside the selected range.
    /// </summary>
    public int PullRequestsMergedInRange =>
        Reports.Count(report => report.IncludeInMetrics && Parameters.IsInRange(report.MergedOn));

    /// <summary>
    /// Gets the number of pull requests rejected inside the selected range.
    /// </summary>
    public int PullRequestsRejectedInRange =>
        Reports.Count(report => report.IncludeInMetrics && Parameters.IsInRange(report.RejectedOn));

    /// <summary>
    /// Gets authored pull request counts per developer for the selected range.
    /// </summary>
    /// <returns>Ordered authored pull request counts per developer.</returns>
    public List<double> GetOpenedPullRequestCountsPerDeveloper() =>
        DeveloperStats.Values
            .Where(static stats => stats.PrsOpenedSince > 0)
            .Select(static stats => (double)stats.PrsOpenedSince)
            .OrderBy(static count => count)
            .ToList();

    /// <summary>
    /// Sorts reports by creation time, ascending.
    /// </summary>
    public void SortReportsByCreatedOn() => Reports.Sort((left, right) => left.CreatedOn.CompareTo(right.CreatedOn));

    public bool IsDeveloperIncluded(DeveloperIdentity identity)
    {
        if (!Parameters.HasTeamFilter)
        {
            return true;
        }

        return _peopleByDisplayName.TryGetValue(identity.DisplayName.Value, out var person) &&
            person.IsInTeam(Parameters.TeamFilter!);
    }

    public bool TryGetPerson(DisplayName displayName, out PersonCsvRow person)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        return _peopleByDisplayName.TryGetValue(displayName.Value, out person);
    }

    public DeveloperStats GetOrAddDeveloper(DeveloperIdentity identity)
    {
        var key = DeveloperKey.FromIdentity(identity);
        if (DeveloperStats.TryGetValue(key, out var existing))
        {
            if (existing.BitbucketUuid is null && identity.Uuid is not null)
            {
                existing.BitbucketUuid = identity.Uuid;
            }

            return existing;
        }

        foreach (var entry in DeveloperStats)
        {
            if (string.Equals(
                entry.Value.DisplayName.Value,
                identity.DisplayName.Value,
                StringComparison.OrdinalIgnoreCase))
            {
                if (entry.Value.BitbucketUuid is null && identity.Uuid is not null)
                {
                    entry.Value.BitbucketUuid = identity.Uuid;
                }

                if (identity.Uuid is not null && entry.Key != key)
                {
                    _ = DeveloperStats.Remove(entry.Key);
                    DeveloperStats[key] = entry.Value;
                }

                return entry.Value;
            }
        }

        var created = new DeveloperStats(identity.DisplayName, identity.Uuid);
        DeveloperStats[key] = created;
        return created;
    }

    private readonly Dictionary<string, PersonCsvRow> _peopleByDisplayName;
}

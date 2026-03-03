
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
            return existing;
        }

        var created = new DeveloperStats(identity.DisplayName);
        DeveloperStats[key] = created;
        return created;
    }

    private readonly Dictionary<string, PersonCsvRow> _peopleByDisplayName;
}

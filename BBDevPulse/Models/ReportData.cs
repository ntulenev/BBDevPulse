
using BBDevPulse.Abstractions;
using BBDevPulse.Math;

namespace BBDevPulse.Models;

/// <summary>
/// Aggregates report output collections.
/// </summary>
public sealed class ReportData
{
    /// <summary>Warning displayed when distinct Bitbucket users share a display name.</summary>
    public const string IdentityCollisionWarning = "Different Bitbucket users share the same display name. This report may not be fully accurate; please verify it manually.";

    private readonly Dictionary<string, HashSet<string>> _observedIdentities = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records a Bitbucket identity before developer statistics can merge it with another identity.</summary>
    public void ObserveDeveloperIdentity(DeveloperIdentity identity)
    {
        if (identity.Uuid is null || string.IsNullOrWhiteSpace(identity.DisplayName.Value))
        {
            return;
        }

        lock (this)
        {
            if (!_observedIdentities.TryGetValue(identity.DisplayName.Value, out var uuids))
            {
                uuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _observedIdentities.Add(identity.DisplayName.Value, uuids);
            }
            uuids.Add(identity.Uuid.Value);
        }
    }

    /// <summary>Returns names observed with more than one distinct UUID.</summary>
    public IReadOnlyList<(string Name, IReadOnlyList<string> Uuids)> GetIdentityCollisions()
    {
        lock (this)
        {
            return _observedIdentities.Where(static entry => entry.Value.Count > 1)
                .OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static entry => (entry.Key, (IReadOnlyList<string>)entry.Value.OrderBy(static uuid => uuid, StringComparer.OrdinalIgnoreCase).ToArray()))
                .ToArray();
        }
    }
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

    public TtfrStatistics GetTtfrStatistics(IStatisticsCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        if (_ttfrStatistics is not null)
        {
            return _ttfrStatistics;
        }

        var days = Reports
            .Where(static report => report.IncludeInMetrics && report.FirstReactionOn.HasValue)
            .Select(report => WorkDurationCalculator.Calculate(report.CreatedOn, report.FirstReactionOn!.Value,
                Parameters.ExcludeWeekend, Parameters.ExcludedDays, Parameters.ForcedIncludeDates).TotalDays)
            .OrderBy(static value => value)
            .ToArray();
        _ttfrStatistics = days.Length == 0
            ? new TtfrStatistics(days, 0, 0)
            : new TtfrStatistics(days, calculator.Percentile(days, 50), calculator.Percentile(days, 75));
        return _ttfrStatistics;
    }

    private TtfrStatistics? _ttfrStatistics;

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
            .Select(static stats => (double)stats.PrsOpenedSince)
            .OrderBy(static count => count)
            .ToList();

    /// <summary>
    /// Gets peer comment counts per developer for the selected range.
    /// </summary>
    /// <returns>Ordered peer comment counts per developer.</returns>
    public List<double> GetPeerCommentCountsPerDeveloper() =>
        DeveloperStats.Values
            .Select(static stats => (double)stats.PeerCommentsAfter)
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
        ObserveDeveloperIdentity(identity);
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

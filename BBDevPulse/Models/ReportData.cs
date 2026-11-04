
using BBDevPulse.Abstractions;
using BBDevPulse.Math;

namespace BBDevPulse.Models;

/// <summary>
/// Aggregates report output collections.
/// </summary>
public sealed class ReportData
{
    /// <summary>Warning displayed when distinct Bitbucket users share a display name.</summary>
    public const string IdentityCollisionWarning = "Different Bitbucket users share the same display name. Team filtering matches names from the CSV, so a matching name can incorrectly include or exclude users and make the filtered report inaccurate.";

    /// <summary>Warning shown when Bitbucket omits a user UUID.</summary>
    public const string MissingUserUuidWarning = "Bitbucket API did not provide a UUID for some users. Those users and their activity are excluded from the report.";

    /// <summary>Whether a user without a UUID was observed.</summary>
    public bool HasMissingUserUuid { get; private set; }

    private readonly Dictionary<string, HashSet<string>> _observedIdentities = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records a Bitbucket identity before developer statistics can merge it with another identity.</summary>
    public void ObserveDeveloperIdentity(DeveloperIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity.DisplayName, nameof(identity));
        EnsureMutable();
        lock (_identityLock)
        {
            if (identity.Uuid is null)
            {
                HasMissingUserUuid = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(identity.DisplayName.Value))
            {
                return;
            }
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
        lock (_identityLock)
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
    /// <param name="peopleByDisplayName">Optional CSV enrichment indexed by display name.</param>
    public ReportData(
        ReportParameters parameters,
        IReadOnlyDictionary<DisplayName, PersonCsvRow>? peopleByDisplayName = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Parameters = parameters;
        DeveloperStats = new System.Collections.ObjectModel.ReadOnlyDictionary<DeveloperKey, DeveloperStats>(_developers);
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
    public IList<PullRequestReport> Reports { get; private set; } = new List<PullRequestReport>();

    /// <summary>Calculates time-to-first-review statistics from the current reports.</summary>
    /// <param name="calculator">Calculator used for the median and 75th percentile.</param>
    /// <returns>Ordered observations and percentile values in days.</returns>
    public TtfrStatistics GetTtfrStatistics(IStatisticsCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var days = Reports
            .Where(static report => report.IncludeInMetrics && report.FirstReactionOn.HasValue)
            .Select(report => WorkDurationCalculator.Calculate(report.CreatedOn, report.FirstReactionOn!.Value,
                Parameters.ExcludeWeekend, Parameters.ExcludedDays, Parameters.ForcedIncludeDates).TotalDays)
            .OrderBy(static value => value)
            .ToArray();
        return days.Length == 0
            ? new TtfrStatistics(days, 0, 0)
            : new TtfrStatistics(days, calculator.Percentile(days, 50), calculator.Percentile(days, 75));
    }

    /// <summary>
    /// Gets the developer statistics keyed by identity.
    /// </summary>
    public IReadOnlyDictionary<DeveloperKey, DeveloperStats> DeveloperStats { get; }

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
    public void SortReportsByCreatedOn()
    {
        EnsureMutable();
        Reports = Reports.OrderBy(static report => report.CreatedOn).ToList();
    }

    /// <summary>Checks whether a developer belongs to the selected team.</summary>
    /// <param name="identity">Developer identity with a display name.</param>
    /// <returns>True when no team filter is active or the developer is in the selected team.</returns>
    public bool IsDeveloperIncluded(DeveloperIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity.DisplayName, nameof(identity));
        if (identity.Uuid is null)
        {
            return false;
        }

        if (!Parameters.HasTeamFilter)
        {
            return true;
        }

        return _peopleByDisplayName.TryGetValue(identity.DisplayName.Value, out var person) &&
            person.IsInTeam(Parameters.TeamFilter!);
    }

    /// <summary>Looks up CSV enrichment by display name.</summary>
    /// <param name="displayName">Display name to look up.</param>
    /// <param name="person">Matched person when found.</param>
    /// <returns>True when the name is present.</returns>
    public bool TryGetPerson(DisplayName displayName, out PersonCsvRow person)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        return _peopleByDisplayName.TryGetValue(displayName.Value, out person);
    }

    /// <summary>Gets or creates the mutable statistics entry for a developer.</summary>
    /// <param name="identity">Developer identity with a display name.</param>
    /// <returns>The current statistics entry.</returns>
    public DeveloperStats GetOrAddDeveloper(DeveloperIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity.DisplayName, nameof(identity));
        ObserveDeveloperIdentity(identity);
        var key = DeveloperKey.FromIdentity(identity);
        if (DeveloperStats.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = new DeveloperStats(identity.DisplayName, identity.Uuid);
        _developers.Add(key, created);
        return created;
    }

    private readonly Dictionary<DeveloperKey, DeveloperStats> _developers = [];
    private readonly Dictionary<string, PersonCsvRow> _peopleByDisplayName;
    internal void ApplyAnalysis(Action update)
    {
        ArgumentNullException.ThrowIfNull(update);
        lock (_identityLock)
        {
            EnsureMutable();
            update();
        }
    }

    /// <summary>Ends accumulation after all analysis tasks finish. The report can then be read concurrently.</summary>
    internal void Freeze()
    {
        if (_frozen)
        {
            return;
        }

        foreach (var stats in DeveloperStats.Values)
        {
            stats.Freeze();
        }

        Reports = Array.AsReadOnly(Reports.ToArray());
        _frozen = true;
    }

    private void EnsureMutable()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Published reports cannot be modified.");
        }
    }

    private bool _frozen;
    private readonly object _identityLock = new();
}

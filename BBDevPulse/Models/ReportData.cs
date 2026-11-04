
using BBDevPulse.Abstractions;
using BBDevPulse.Logic;
using BBDevPulse.Math;

namespace BBDevPulse.Models;

/// <summary>
/// Aggregates report output collections.
/// </summary>
public sealed class ReportData
{
    /// <summary>Warning displayed when distinct Bitbucket users share a display name.</summary>
    public const string IdentityCollisionWarning = "Different Bitbucket users share the same display name.";

    /// <summary>Warning shown when Bitbucket omits a user UUID.</summary>
    public const string MissingUserUuidWarning = "Bitbucket API did not provide a UUID for some users. Those users and their activity are excluded from the report.";

    /// <summary>Whether a user without a UUID was observed.</summary>
    public bool HasMissingUserUuid { get; private set; }

    private readonly Dictionary<string, HashSet<string>> _observedIdentities = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records a Bitbucket identity before developer statistics can merge it with another identity.</summary>
    internal void ObserveDeveloperIdentity(DeveloperIdentity identity)
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
        : this(parameters, peopleByDisplayName, deferResults: false)
    {
    }

    internal ReportData(ReportParameters parameters,
        IReadOnlyDictionary<DisplayName, PersonCsvRow>? peopleByDisplayName, bool deferResults)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Parameters = parameters;
        _deferResults = deferResults;
        Reports = new System.Collections.ObjectModel.ReadOnlyCollection<PullRequestReport>(_reports);
        DeveloperStats = new System.Collections.ObjectModel.ReadOnlyDictionary<DeveloperKey, DeveloperStats>(_developers);
        _peopleByDisplayName = peopleByDisplayName is null
            ? new Dictionary<string, PersonCsvRow>(StringComparer.Ordinal)
            : peopleByDisplayName.ToDictionary(
                static entry => entry.Key.Value,
                static entry => entry.Value with
                {
                    AdditionalRows = Array.AsReadOnly(entry.Value.Rows.Skip(1)
                        .Select(static row => row with { AdditionalRows = null }).ToArray())
                },
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the report parameters.
    /// </summary>
    public ReportParameters Parameters { get; }

    /// <summary>
    /// Gets the pull request reports.
    /// </summary>
    public IReadOnlyList<PullRequestReport> Reports { get; private set; } = null!;

    private IList<PullRequestReport> _reports = new List<PullRequestReport>();

    internal IList<PullRequestReport> MutableReports {
        get
        {
            EnsureMutable();
            return _reports;
        }
    }

    private void SetReports(IEnumerable<PullRequestReport> reports)
    {
        _reports = reports.ToList();
        Reports = new System.Collections.ObjectModel.ReadOnlyCollection<PullRequestReport>(_reports);
    }

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
    internal void SortReportsByCreatedOn()
    {
        EnsureMutable();
        SetReports(Reports.OrderBy(static report => report.CreatedOn));
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

        if (!_peopleByDisplayName.TryGetValue(identity.DisplayName.Value, out var entries))
        {
            return false;
        }

        var exact = entries.Rows.Where(row => row.MatchesUuid(identity.Uuid)).ToArray();
        return exact.Length > 0
            ? exact.Any(row => row.IsInTeam(Parameters.TeamFilter!))
            : entries.Rows.Any(row => string.IsNullOrWhiteSpace(row.Uuid) && row.IsInTeam(Parameters.TeamFilter!));
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

    /// <summary>Resolves CSV metadata by name and, when provided, UUID.</summary>
    public bool TryGetPerson(DeveloperIdentity identity, out PersonCsvRow person)
    {
        ArgumentNullException.ThrowIfNull(identity.DisplayName, nameof(identity));
        person = default;
        if (identity.Uuid is null || !_peopleByDisplayName.TryGetValue(identity.DisplayName.Value, out var rows))
        {
            return false;
        }

        foreach (var row in rows.Rows)
        {
            if (row.MatchesUuid(identity.Uuid))
            {
                person = row;
                return true;
            }
        }
        var fallbackRows = rows.Rows.Where(row => string.IsNullOrWhiteSpace(row.Uuid)).ToArray();
        if (fallbackRows.Length == 1)
        {
            person = fallbackRows[0];
            return true;
        }
        return false;
    }

    /// <summary>Classifies duplicate-name warnings for the selected team.</summary>
    public IReadOnlyList<IdentityCollisionNotice> GetIdentityCollisionNotices() =>
        GetIdentityCollisions().Select(collision =>
        {
            var rows = _peopleByDisplayName
                .Where(entry => string.Equals(entry.Key, collision.Name, StringComparison.OrdinalIgnoreCase))
                .SelectMany(entry => entry.Value.Rows).ToArray();
            var selected = Parameters.HasTeamFilter
                ? rows.Where(row => row.IsInTeam(Parameters.TeamFilter!)).ToArray()
                : [];
            var unresolved = selected.Any(row => string.IsNullOrWhiteSpace(row.Uuid));
            var message = !Parameters.HasTeamFilter
                ? "No team filter is selected. CSV enrichment may still be ambiguous when UUID is missing."
                : selected.Length == 0
                    ? "This name is not in the selected team in the CSV, so it does not affect team selection."
                    : unresolved
                        ? "This name is in the selected team in the CSV without a UUID. Team filtering can include the wrong user and make the report inaccurate."
                        : "This name is in the selected team in the CSV with UUIDs. The UUIDs resolve team selection for these entries.";
            return new IdentityCollisionNotice(collision.Name, collision.Uuids, unresolved, message);
        }).ToArray();

    /// <summary>Gets or creates the mutable statistics entry for a developer.</summary>
    /// <param name="identity">Developer identity with a display name.</param>
    /// <returns>The current statistics entry.</returns>
    internal DeveloperStats GetOrAddDeveloper(DeveloperIdentity identity)
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
    private readonly HashSet<(string Workspace, string Repository, int Id)> _appliedPullRequests = [];
    private readonly HashSet<(string Workspace, string Repository, int Id)> _queuedPullRequests = [];
    private readonly List<PullRequestAnalysisResult> _pendingResults = [];
    private bool _deferResults;
    private bool _analysisFailed;

    internal void ApplyResult(PullRequestAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (_identityLock)
        {
            EnsureMutable();
            if (!string.Equals(result.Workspace.Value, Parameters.Workspace.Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The pull request belongs to a different workspace.", nameof(result));
            }
            var key = (result.Workspace.Value.ToUpperInvariant(), result.Repository.Value.ToUpperInvariant(), result.Id.Value);
            if (_appliedPullRequests.Contains(key) || _queuedPullRequests.Contains(key))
            {
                throw new InvalidOperationException("The pull request result has already been applied.");
            }
            if (_deferResults)
            {
                _queuedPullRequests.Add(key);
                _pendingResults.Add(result);
                return;
            }
            ValidateResult(result);
            _appliedPullRequests.Add(key);

            foreach (var identity in result.ObservedIdentities)
            {
                ObserveDeveloperIdentity(identity);
            }
            if (result.Report is null)
            {
                return;
            }

            var report = result.Report;
            if (result.Author.HasValue && result.IncludeAuthoredPullRequest)
            {
                var stats = GetOrAddDeveloper(result.Author.Value);
                if (Parameters.IsInRange(result.CreatedOn))
                {
                    stats.PrsOpenedSince++;
                }
                if (Parameters.IsInRange(result.MergedOn))
                {
                    stats.PrsMergedAfter++;
                }
                if (result.HasCommits)
                {
                    stats.Corrections += result.Corrections;
                }
                else
                {
                    stats.HasCompleteCorrectionsData = false;
                }
                if (result.Comments is not null)
                {
                    stats.MutableAuthoredPullRequests.Add(report);
                    foreach (var commit in result.Commits)
                    {
                        stats.MutableCommitActivities.Add(commit);
                    }
                }
            }

            foreach (var entry in result.CommentCounts)
            {
                if (result.Participants.TryGetValue(entry.Key, out var participant) && IsDeveloperIncluded(participant))
                {
                    var stats = GetOrAddDeveloper(participant);
                    stats.CommentsAfter += entry.Value;
                    if (result.Author.HasValue && !participant.IsSameIdentity(result.Author.Value))
                    {
                        stats.PeerCommentsAfter += entry.Value;
                    }
                }
            }
            foreach (var entry in result.ApprovalCounts)
            {
                if (result.Participants.TryGetValue(entry.Key, out var participant) && IsDeveloperIncluded(participant))
                {
                    GetOrAddDeveloper(participant).ApprovalsAfter += entry.Value;
                }
            }
            if (result.Comments is not null)
            {
                foreach (var detail in result.Comments)
                {
                    if (IsDeveloperIncluded(detail.User))
                    {
                        GetOrAddDeveloper(detail.User).MutableCommentActivities.Add(new DeveloperCommentActivity(
                            report.Repository, report.RepositorySlug, result.Id, report.Author, detail.Date));
                    }
                }
                foreach (var detail in result.Approvals!)
                {
                    if (IsDeveloperIncluded(detail.User))
                    {
                        GetOrAddDeveloper(detail.User).MutableApprovalActivities.Add(new DeveloperApprovalActivity(
                            report.Repository, report.RepositorySlug, result.Id, report.Author, detail.Date));
                    }
                }
            }
            MutableReports.Add(report);
        }
    }

    private void ValidateResult(PullRequestAnalysisResult result)
    {
        foreach (var identity in result.ObservedIdentities)
        {
            ArgumentNullException.ThrowIfNull(identity.DisplayName, nameof(result));
        }
        if (result.Report is null)
        {
            return;
        }

        var increments = new Dictionary<DeveloperKey, PendingCounts>();
        PendingCounts For(DeveloperIdentity identity)
        {
            var key = DeveloperKey.FromIdentity(identity);
            if (!increments.TryGetValue(key, out var value))
            {
                value = new PendingCounts();
                increments.Add(key, value);
            }
            return value;
        }

        if (result.Author.HasValue && result.IncludeAuthoredPullRequest)
        {
            var pending = For(result.Author.Value);
            pending.Opened = Parameters.IsInRange(result.CreatedOn) ? 1 : 0;
            pending.Merged = Parameters.IsInRange(result.MergedOn) ? 1 : 0;
            pending.Corrections = result.HasCommits ? result.Corrections : 0;
            ArgumentOutOfRangeException.ThrowIfNegative(pending.Corrections);
        }
        foreach (var entry in result.CommentCounts)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(entry.Value);
            if (result.Participants.TryGetValue(entry.Key, out var participant) && IsDeveloperIncluded(participant))
            {
                var pending = For(participant);
                pending.Comments = checked(pending.Comments + entry.Value);
                if (result.Author.HasValue && !participant.IsSameIdentity(result.Author.Value))
                {
                    pending.PeerComments = checked(pending.PeerComments + entry.Value);
                }
            }
        }
        foreach (var entry in result.ApprovalCounts)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(entry.Value);
            if (result.Participants.TryGetValue(entry.Key, out var participant) && IsDeveloperIncluded(participant))
            {
                var pending = For(participant);
                pending.Approvals = checked(pending.Approvals + entry.Value);
            }
        }
        if (result.Comments is not null)
        {
            foreach (var detail in result.Comments)
                ArgumentNullException.ThrowIfNull(detail.User.DisplayName, nameof(result));
            foreach (var detail in result.Approvals!)
                ArgumentNullException.ThrowIfNull(detail.User.DisplayName, nameof(result));
        }
        foreach (var (key, delta) in increments)
        {
            if (_developers.TryGetValue(key, out var stats))
            {
                _ = checked(stats.PrsOpenedSince + delta.Opened);
                _ = checked(stats.PrsMergedAfter + delta.Merged);
                _ = checked(stats.Corrections + delta.Corrections);
                _ = checked(stats.CommentsAfter + delta.Comments);
                _ = checked(stats.PeerCommentsAfter + delta.PeerComments);
                _ = checked(stats.ApprovalsAfter + delta.Approvals);
            }
        }
    }

    private sealed class PendingCounts
    {
        public int Opened { get; set; }
        public int Merged { get; set; }
        public int Corrections { get; set; }
        public int Comments { get; set; }
        public int PeerComments { get; set; }
        public int Approvals { get; set; }
    }

    internal void CompleteAnalysis(CancellationToken cancellationToken = default)
    {
        lock (_identityLock)
        {
            EnsureMutable();
            if (!_deferResults)
            {
                return;
            }
            _deferResults = false;
            var ordered = _pendingResults.OrderBy(static result => result.CreatedOn)
                .ThenBy(static result => result.Repository.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static result => result.Id.Value).ToArray();
            _pendingResults.Clear();
            _queuedPullRequests.Clear();
            try
            {
                foreach (var result in ordered)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplyResult(result);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                _analysisFailed = true;
                throw;
            }
        }
    }

    internal ReportData CreatePublishedSnapshot()
    {
        lock (_identityLock)
        {
            EnsureMutable();
            var people = _peopleByDisplayName.ToDictionary(
                static entry => new DisplayName(entry.Key), static entry => entry.Value);
            var snapshot = new ReportData(Parameters, people)
            {
                HasMissingUserUuid = HasMissingUserUuid
            };
            snapshot.SetReports(Reports.OrderBy(static report => report.CreatedOn)
                .ThenBy(static report => report.RepositorySlug, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static report => report.Id.Value));
            foreach (var entry in _developers.OrderBy(static entry => entry.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                snapshot._developers.Add(entry.Key, entry.Value.Copy());
            }
            foreach (var entry in _observedIdentities)
            {
                snapshot._observedIdentities.Add(entry.Key, new HashSet<string>(entry.Value, StringComparer.OrdinalIgnoreCase));
            }
            snapshot.Freeze();
            Freeze();
            return snapshot;
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

        SetReports(Reports);
        _frozen = true;
    }

    private void EnsureMutable()
    {
        if (_analysisFailed)
        {
            throw new InvalidOperationException("A failed report analysis cannot be published or modified.");
        }
        if (_frozen)
        {
            throw new InvalidOperationException("Published reports cannot be modified.");
        }
    }

    private bool _frozen;
    private readonly object _identityLock = new();
}

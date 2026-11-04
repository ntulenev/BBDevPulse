using BBDevPulse.Abstractions;
using BBDevPulse.Logic;

namespace BBDevPulse.Models;

/// <summary>A detached, read-only report published after analysis completes.</summary>
public sealed class ReportData
{
    /// <summary>Warning about users with identical names.</summary>
    public const string IdentityCollisionWarning = ReportBuildState.IdentityCollisionWarning;
    /// <summary>Warning about missing Bitbucket user UUIDs.</summary>
    public const string MissingUserUuidWarning = ReportBuildState.MissingUserUuidWarning;

    private readonly ReportBuildState _state;

    internal ReportData(ReportBuildState state) => _state = state;
    internal ReportData(ReportParameters parameters, IReadOnlyDictionary<DisplayName, PersonCsvRow>? people = null)
        : this(new ReportBuildState(parameters, people)) { }

    /// <summary>Inputs used to generate this report.</summary>
    public ReportParameters Parameters => _state.Parameters;
    /// <summary>Pull request rows.</summary>
    public IReadOnlyList<PullRequestReport> Reports => _state.Reports;
    /// <summary>Developer statistics by UUID.</summary>
    public IReadOnlyDictionary<DeveloperKey, DeveloperStats> DeveloperStats => _state.DeveloperStats;
    /// <summary>Whether an identity without a UUID was observed.</summary>
    public bool HasMissingUserUuid => _state.HasMissingUserUuid;
    /// <summary>Number of PRs created in the period.</summary>
    public int PullRequestsCreatedInRange => _state.PullRequestsCreatedInRange;
    /// <summary>Number of PRs merged in the period.</summary>
    public int PullRequestsMergedInRange => _state.PullRequestsMergedInRange;
    /// <summary>Number of PRs rejected in the period.</summary>
    public int PullRequestsRejectedInRange => _state.PullRequestsRejectedInRange;
    /// <summary>Calculates time to first reaction.</summary>
    public TtfrStatistics GetTtfrStatistics(IStatisticsCalculator calculator) => _state.GetTtfrStatistics(calculator);
    /// <summary>Returns ordered counts of opened PRs.</summary>
    public List<double> GetOpenedPullRequestCountsPerDeveloper() => _state.GetOpenedPullRequestCountsPerDeveloper();
    /// <summary>Returns ordered peer comment counts.</summary>
    public List<double> GetPeerCommentCountsPerDeveloper() => _state.GetPeerCommentCountsPerDeveloper();
    /// <summary>Returns names shared by different UUIDs.</summary>
    public IReadOnlyList<(string Name, IReadOnlyList<string> Uuids)> GetIdentityCollisions() => _state.GetIdentityCollisions();
    /// <summary>Returns notices for name collisions.</summary>
    public IReadOnlyList<IdentityCollisionNotice> GetIdentityCollisionNotices() => _state.GetIdentityCollisionNotices();
    /// <summary>Returns observed name discrepancies for matching UUIDs.</summary>
    /// <returns>UUIDs and their differing Bitbucket and CSV names.</returns>
    public IReadOnlyList<(string Uuid, string BitbucketName, string CsvName)> GetNameMismatches() => _state.GetNameMismatches();
    /// <summary>Checks whether an identity belongs to the selected team.</summary>
    public bool IsDeveloperIncluded(DeveloperIdentity identity) => _state.IsDeveloperIncluded(identity);
    /// <summary>Looks up CSV metadata by name.</summary>
    public bool TryGetPerson(DisplayName name, out PersonCsvRow person) => _state.TryGetPerson(name, out person);
    /// <summary>Looks up CSV metadata by UUID.</summary>
    public bool TryGetPerson(DeveloperIdentity identity, out PersonCsvRow person) => _state.TryGetPerson(identity, out person);

    // Internal fixture support. Published instances wrap a frozen, detached state.
    internal IList<PullRequestReport> MutableReports => _state.MutableReports;
    internal DeveloperStats GetOrAddDeveloper(DeveloperIdentity identity) => _state.GetOrAddDeveloper(identity);
    internal void ObserveDeveloperIdentity(DeveloperIdentity identity) => _state.ObserveDeveloperIdentity(identity);
    internal void ApplyResult(PullRequestAnalysisResult result) => _state.ApplyResult(result);
    internal void SortReportsByCreatedOn() => _state.SortReportsByCreatedOn();
    internal void Freeze() => _state.Freeze();
}

using BBDevPulse.Models;

namespace BBDevPulse.Logic;

// Owns the mutable report during collection and publishes it exactly once.
internal sealed class ReportAccumulator
{
    private ReportBuildState? _workingData;

    public ReportAccumulator(ReportParameters parameters,
        IReadOnlyDictionary<DisplayName, PersonCsvRow>? peopleByDisplayName = null,
        bool deferResults = true)
    {
        _workingData = new ReportBuildState(parameters, peopleByDisplayName, deferResults);
    }

    internal static ReportAccumulator CreateImmediate(ReportParameters parameters,
        IReadOnlyDictionary<DisplayName, PersonCsvRow>? people = null) => new(parameters, people, false);

    public ReportBuildState WorkingData => _workingData ??
        throw new InvalidOperationException("The report has already been published.");

    public ReportData Build(CancellationToken cancellationToken = default)
    {
        CompleteAnalysis(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var report = WorkingData.CreatePublishedSnapshot();
        _workingData = null;
        return new ReportData(report);
    }

    internal IReadOnlyList<PullRequestReport> Reports => WorkingData.Reports;
    internal IReadOnlyDictionary<DeveloperKey, DeveloperStats> DeveloperStats => WorkingData.DeveloperStats;
    internal bool HasMissingUserUuid => WorkingData.HasMissingUserUuid;
    internal IReadOnlyList<(string Name, IReadOnlyList<string> Uuids)> GetIdentityCollisions() => WorkingData.GetIdentityCollisions();
    internal IReadOnlyList<IdentityCollisionNotice> GetIdentityCollisionNotices() => WorkingData.GetIdentityCollisionNotices();

    public void RegisterDeveloper(DeveloperIdentity identity) => WorkingData.GetOrAddDeveloper(identity);

    public ReportParameters Parameters => WorkingData.Parameters;
    public bool IsDeveloperIncluded(DeveloperIdentity identity) => WorkingData.IsDeveloperIncluded(identity);
    public void ApplyResult(PullRequestAnalysisResult result) => WorkingData.ApplyResult(result);

    public void EnrichDevelopers()
    {
        foreach (var stat in WorkingData.DeveloperStats.Values)
        {
            if (WorkingData.TryGetPerson(new DeveloperIdentity(stat.BitbucketUuid, stat.DisplayName), out var person))
            {
                stat.Grade = person.Grade;
                stat.Department = person.Department;
            }
        }
    }

    public void CompleteAnalysis(CancellationToken cancellationToken = default) =>
        WorkingData.CompleteAnalysis(cancellationToken);
}

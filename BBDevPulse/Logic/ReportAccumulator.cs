using BBDevPulse.Models;

namespace BBDevPulse.Logic;

// Owns the mutable report during collection and publishes it exactly once.
internal sealed class ReportAccumulator
{
    private ReportData? _workingData;

    public ReportAccumulator(ReportParameters parameters,
        IReadOnlyDictionary<DisplayName, PersonCsvRow>? peopleByDisplayName = null)
    {
        _workingData = new ReportData(parameters, peopleByDisplayName);
    }

    public ReportData WorkingData => _workingData ??
        throw new InvalidOperationException("The report has already been published.");

    public ReportData Build()
    {
        var report = WorkingData;
        report.Freeze();
        _workingData = null;
        return report;
    }
}

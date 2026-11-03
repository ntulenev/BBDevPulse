using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;

namespace BBDevPulse.Logic;

/// <summary>
/// Renders the built-in synthetic report without resolving Bitbucket or CSV services.
/// </summary>
internal sealed class DemoReportRunner : IReportRunner
{
    public DemoReportRunner(
        IReportPresenter presenter,
        IHtmlReportRenderer htmlReportRenderer,
        IPdfReportRenderer pdfReportRenderer)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(htmlReportRenderer);
        ArgumentNullException.ThrowIfNull(pdfReportRenderer);
        _presenter = presenter;
        _htmlReportRenderer = htmlReportRenderer;
        _pdfReportRenderer = pdfReportRenderer;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var peopleByName = await DemoPeopleCsv.LoadAsync(cancellationToken).ConfigureAwait(false);
        var teams = TeamFilterCatalog.Create(peopleByName);
        var teamFilter = teams.Count == 0 ? null : _presenter.SelectTeamFilter(teams, peopleByName.Count);
        var reportData = DemoReportData.Create(peopleByName, teamFilter);
        var htmlName = Path.GetFileName(DemoReportOptions.Html.ResolveOutputPath(teamFilter));
        var pdfName = Path.GetFileName(DemoReportOptions.Pdf.ResolveOutputPath(teamFilter));
        _presenter.RenderDemoSettings(reportData.Parameters, htmlName, pdfName);
        if (!_presenter.ConfirmDemoStart())
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _presenter.RenderReport(reportData);
        await _htmlReportRenderer.RenderReportAsync(reportData, cancellationToken).ConfigureAwait(false);
        await _pdfReportRenderer.RenderReportAsync(reportData, cancellationToken).ConfigureAwait(false);
    }

    private readonly IReportPresenter _presenter;
    private readonly IHtmlReportRenderer _htmlReportRenderer;
    private readonly IPdfReportRenderer _pdfReportRenderer;
}

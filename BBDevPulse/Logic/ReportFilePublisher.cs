using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.Logic;

/// <summary>Writes the configured report files in publication order.</summary>
internal sealed class ReportFilePublisher
{
    private readonly IHtmlReportRenderer _html;
    private readonly IPdfReportRenderer _pdf;

    public ReportFilePublisher(IHtmlReportRenderer html, IPdfReportRenderer pdf)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(pdf);
        _html = html;
        _pdf = pdf;
    }

    public async Task PublishAsync(ReportData report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        await _html.RenderReportAsync(report, cancellationToken).ConfigureAwait(false);
        await _pdf.RenderReportAsync(report, cancellationToken).ConfigureAwait(false);
    }
}

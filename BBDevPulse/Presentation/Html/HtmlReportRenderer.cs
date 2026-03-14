using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Presentation.Html;

/// <summary>
/// HTML implementation for BBDevPulse report rendering.
/// </summary>
public sealed class HtmlReportRenderer : IHtmlReportRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlReportRenderer"/> class.
    /// </summary>
    /// <param name="options">Bitbucket options.</param>
    /// <param name="htmlReportFileStore">HTML output file store.</param>
    /// <param name="htmlContentComposer">HTML content composer.</param>
    /// <param name="htmlReportLauncher">HTML report launcher.</param>
    public HtmlReportRenderer(
        IOptions<BitbucketOptions> options,
        IHtmlReportFileStore htmlReportFileStore,
        IHtmlContentComposer htmlContentComposer,
        IHtmlReportLauncher htmlReportLauncher)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(htmlReportFileStore);
        ArgumentNullException.ThrowIfNull(htmlContentComposer);
        ArgumentNullException.ThrowIfNull(htmlReportLauncher);

        _htmlOptions = options.Value.Html ?? new HtmlOptions();
        _htmlReportFileStore = htmlReportFileStore;
        _htmlContentComposer = htmlContentComposer;
        _htmlReportLauncher = htmlReportLauncher;
    }

    /// <inheritdoc />
    public async Task RenderReportAsync(ReportData reportData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportData);

        if (!_htmlOptions.Enabled)
        {
            return;
        }

        var outputPath = _htmlOptions.ResolveOutputPath();
        var html = _htmlContentComposer.Compose(reportData);

        await _htmlReportFileStore.SaveAsync(outputPath, html, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"HTML report saved to: {outputPath}");

        if (!_htmlOptions.OpenInBrowser)
        {
            return;
        }

        _htmlReportLauncher.Open(outputPath);
        Console.WriteLine($"HTML report opened in browser: {outputPath}");
    }

    private readonly HtmlOptions _htmlOptions;
    private readonly IHtmlReportFileStore _htmlReportFileStore;
    private readonly IHtmlContentComposer _htmlContentComposer;
    private readonly IHtmlReportLauncher _htmlReportLauncher;
}

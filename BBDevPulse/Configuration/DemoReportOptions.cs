namespace BBDevPulse.Configuration;

/// <summary>
/// Safe, fixed output settings for synthetic reports.
/// </summary>
internal static class DemoReportOptions
{
    public static HtmlOptions Html { get; } = new()
    {
        Enabled = true,
        OutputPath = "bbdevpulse-demo-report.html",
        OpenInBrowser = false
    };

    public static PdfOptions Pdf { get; } = new()
    {
        Enabled = true,
        OutputPath = "bbdevpulse-demo-report.pdf"
    };
}

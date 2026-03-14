namespace BBDevPulse.Presentation.Html;

/// <summary>
/// Loads embedded HTML templates used by the report composer.
/// </summary>
internal static class HtmlTemplateLoader
{
    private static readonly Lazy<string> ReportTemplate = new(() => LoadTemplate("BBDevPulse.HtmlTemplates.ReportDocument.html"));

    public static string LoadReportTemplate() => ReportTemplate.Value;

    private static string LoadTemplate(string resourceName)
    {
        var assembly = typeof(HtmlTemplateLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded HTML template '{resourceName}' was not found in assembly '{assembly.GetName().Name}'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

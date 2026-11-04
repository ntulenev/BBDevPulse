namespace BBDevPulse.Presentation.Formatters;

/// <summary>Formats a CSV and Bitbucket name discrepancy for report output.</summary>
internal static class NameMismatchFormatter
{
    /// <summary>Formats the UUID and both names for display.</summary>
    /// <param name="mismatch">The names observed for a UUID.</param>
    /// <returns>A warning suitable for report output.</returns>
    public static string Format((string Uuid, string BitbucketName, string CsvName) mismatch) =>
        $"CSV name mismatch (UUID: {mismatch.Uuid}): " +
        $"Bitbucket: {mismatch.BitbucketName}; CSV: {mismatch.CsvName}";
}

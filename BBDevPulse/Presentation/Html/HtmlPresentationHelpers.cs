using System.Globalization;
using System.Net;

namespace BBDevPulse.Presentation.Html;

/// <summary>
/// Helper methods for HTML report rendering.
/// </summary>
internal static class HtmlPresentationHelpers
{
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static string EncodeAttribute(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty).Replace("'", "&#39;", StringComparison.Ordinal);

    public static string FormatDate(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTimeOffset? value) =>
        value.HasValue ? FormatDateTime(value.Value) : "-";

    public static string BuildRepositoryUrl(string workspace, string repositorySlug) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "https://bitbucket.org/{0}/{1}/",
            Uri.EscapeDataString(workspace),
            Uri.EscapeDataString(repositorySlug));

    public static string BuildPullRequestUrl(string workspace, string repositorySlug, int pullRequestId) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "https://bitbucket.org/{0}/{1}/pull-requests/{2}",
            Uri.EscapeDataString(workspace),
            Uri.EscapeDataString(repositorySlug),
            pullRequestId.ToString(CultureInfo.InvariantCulture));

    public static string BuildCommitUrl(string workspace, string repositorySlug, string commitHash) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "https://bitbucket.org/{0}/{1}/commits/{2}",
            Uri.EscapeDataString(workspace),
            Uri.EscapeDataString(repositorySlug),
            Uri.EscapeDataString(commitHash));

    public static string ShortCommitHash(string commitHash) =>
        commitHash.Length <= 12
            ? commitHash
            : commitHash[..12];

    public static string TrimCommitMessage(string message) =>
        message
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.Caching;

// The fingerprint encoding is part of the version 2 cache compatibility contract.
internal sealed class PullRequestAnalysisCacheKeyFactory : IPullRequestAnalysisCacheKeyFactory
{
    public string BuildParametersFingerprint(ReportParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var rawFingerprint = string.Join(
            '\n',
            parameters.FilterDate.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            parameters.ToDateExclusive?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            parameters.ShowAllDetailsForDevelopers ? "1" : "0");

        return ComputeFingerprint(rawFingerprint);
    }

    public string BuildPullRequestFingerprint(PullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        var rawFingerprint = string.Join(
            '\n',
            pullRequest.Id.Value.ToString(CultureInfo.InvariantCulture),
            pullRequest.State.ToString(),
            pullRequest.CreatedOn.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            pullRequest.UpdatedOn?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            pullRequest.ClosedOn?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            pullRequest.MergedOn?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            pullRequest.Author?.DisplayName.Value ?? string.Empty,
            pullRequest.Author?.Uuid?.Value ?? string.Empty,
            pullRequest.Destination?.Branch?.Name ?? string.Empty);

        return ComputeFingerprint(rawFingerprint);
    }

    private static string ComputeFingerprint(string value)
    {
        var fingerprintBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(fingerprintBytes);
    }

}

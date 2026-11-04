using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>Builds fingerprints identifying cached pull request inputs.</summary>
public interface IPullRequestAnalysisCacheKeyFactory
{
    /// <summary>Builds a fingerprint for the report inputs used by cached data.</summary>
    string BuildParametersFingerprint(ReportParameters parameters);

    /// <summary>Builds a fingerprint for the current pull request.</summary>
    string BuildPullRequestFingerprint(PullRequest pullRequest);
}

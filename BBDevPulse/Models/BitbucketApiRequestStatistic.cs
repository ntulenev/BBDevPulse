namespace BBDevPulse.Models;

/// <summary>
/// Aggregated statistics for a single Bitbucket API endpoint.
/// </summary>
/// <param name="ApiName">Normalized Bitbucket API name.</param>
/// <param name="RequestCount">Number of requests sent to this API.</param>
public sealed record BitbucketApiRequestStatistic(string ApiName, int RequestCount);

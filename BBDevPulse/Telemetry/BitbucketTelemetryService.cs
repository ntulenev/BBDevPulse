using System.Collections.Concurrent;
using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Telemetry;

/// <summary>
/// Thread-safe Bitbucket request and cache telemetry collector.
/// </summary>
internal sealed class BitbucketTelemetryService : IBitbucketTelemetryService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTelemetryService"/> class.
    /// </summary>
    /// <param name="options">Bitbucket configuration options.</param>
    public BitbucketTelemetryService(IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _isEnabled = options.Value.Telemetry.Enabled;
    }

    /// <inheritdoc />
    public void TrackRequest(Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);

        if (!_isEnabled)
        {
            return;
        }

        var apiName = NormalizeApiName(requestUri);
        _ = _requestCounts.AddOrUpdate(
            apiName,
            static _ => 1,
            static (_, currentCount) => currentCount + 1);
    }

    /// <inheritdoc />
    public void TrackAnalysisSnapshotCacheHit(PullRequestAnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!_isEnabled)
        {
            return;
        }

        _ = Interlocked.Increment(ref _analysisSnapshotCacheHits);
        _ = Interlocked.Add(ref _estimatedAvoidedRequests, EstimateAvoidedRequests(snapshot));
    }

    /// <inheritdoc />
    public void TrackAnalysisSnapshotCacheMiss()
    {
        if (!_isEnabled)
        {
            return;
        }

        _ = Interlocked.Increment(ref _analysisSnapshotCacheMisses);
    }

    /// <inheritdoc />
    public void TrackAnalysisSnapshotCacheStore()
    {
        if (!_isEnabled)
        {
            return;
        }

        _ = Interlocked.Increment(ref _analysisSnapshotCacheStores);
    }

    /// <inheritdoc />
    public BitbucketTelemetrySnapshot GetSnapshot()
    {
        if (!_isEnabled)
        {
            return new BitbucketTelemetrySnapshot(false, 0, 0, 0, 0, 0, []);
        }

        var requestStatistics = _requestCounts
            .Select(static pair => new BitbucketApiRequestStatistic(pair.Key, pair.Value))
            .OrderByDescending(static statistic => statistic.RequestCount)
            .ThenBy(static statistic => statistic.ApiName, StringComparer.Ordinal)
            .ToArray();

        return new BitbucketTelemetrySnapshot(
            true,
            requestStatistics.Sum(static statistic => statistic.RequestCount),
            _analysisSnapshotCacheHits,
            _analysisSnapshotCacheMisses,
            _analysisSnapshotCacheStores,
            _estimatedAvoidedRequests,
            requestStatistics);
    }

    private static int EstimateAvoidedRequests(PullRequestAnalysisSnapshot snapshot)
    {
        var avoidedRequests = 1;
        if (snapshot.HasEnrichment)
        {
            avoidedRequests += 2;
            avoidedRequests += snapshot.CommitActivities.Count;
        }

        return avoidedRequests;
    }

    private static string NormalizeApiName(Uri requestUri)
    {
        var requestTarget = requestUri.IsAbsoluteUri ? requestUri.PathAndQuery : requestUri.OriginalString;
        var requestTargetParts = requestTarget.Split('?', count: 2, StringSplitOptions.TrimEntries);
        var path = requestTargetParts[0].Trim('/');

        if (string.IsNullOrWhiteSpace(path))
        {
            return "GET /";
        }

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (segments.Count > 0 && segments[0].Equals("2.0", StringComparison.OrdinalIgnoreCase))
        {
            segments.RemoveAt(0);
        }

        if (segments.Count == 1 && segments[0].Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return "GET /user";
        }

        if (segments.Count == 2 && segments[0].Equals("repositories", StringComparison.OrdinalIgnoreCase))
        {
            return "GET /repositories/{workspace}";
        }

        if (segments.Count == 4
            && segments[0].Equals("repositories", StringComparison.OrdinalIgnoreCase)
            && segments[3].Equals("pullrequests", StringComparison.OrdinalIgnoreCase))
        {
            return "GET /repositories/{workspace}/{repository}/pullrequests";
        }

        if (segments.Count == 5
            && segments[0].Equals("repositories", StringComparison.OrdinalIgnoreCase)
            && segments[3].Equals("pullrequests", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[4], NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return "GET /repositories/{workspace}/{repository}/pullrequests/{pullRequestId}";
        }

        if (segments.Count == 6
            && segments[0].Equals("repositories", StringComparison.OrdinalIgnoreCase)
            && segments[3].Equals("pullrequests", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[4], NumberStyles.None, CultureInfo.InvariantCulture, out _)
            && segments[5].Equals("activity", StringComparison.OrdinalIgnoreCase))
        {
            return "GET /repositories/{workspace}/{repository}/pullrequests/{pullRequestId}/activity";
        }

        if (segments.Count == 6
            && segments[0].Equals("repositories", StringComparison.OrdinalIgnoreCase)
            && segments[3].Equals("pullrequests", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[4], NumberStyles.None, CultureInfo.InvariantCulture, out _)
            && segments[5].Equals("commits", StringComparison.OrdinalIgnoreCase))
        {
            return "GET /repositories/{workspace}/{repository}/pullrequests/{pullRequestId}/commits";
        }

        if (segments.Count >= 4
            && segments[0].Equals("repositories", StringComparison.OrdinalIgnoreCase)
            && segments[3].Equals("diffstat", StringComparison.OrdinalIgnoreCase))
        {
            return "GET /repositories/{workspace}/{repository}/diffstat";
        }

        return $"GET /{string.Join('/', segments)}";
    }

    private readonly ConcurrentDictionary<string, int> _requestCounts = new(StringComparer.Ordinal);
    private readonly bool _isEnabled;
    private int _analysisSnapshotCacheHits;
    private int _analysisSnapshotCacheMisses;
    private int _analysisSnapshotCacheStores;
    private int _estimatedAvoidedRequests;
}

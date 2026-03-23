using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using BBDevPulse.Abstractions;
using BBDevPulse.Caching.Internal.Models;
using BBDevPulse.Models;

namespace BBDevPulse.Caching;

/// <summary>
/// File-based cache for pull request analysis snapshots.
/// </summary>
internal sealed class FilePullRequestAnalysisCache : IPullRequestAnalysisCache
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FilePullRequestAnalysisCache"/> class.
    /// </summary>
    /// <param name="mapper">Cache snapshot mapper.</param>
    /// <param name="telemetryService">Telemetry service.</param>
    /// <param name="cacheRootDirectory">Optional custom cache directory.</param>
    public FilePullRequestAnalysisCache(
        IPullRequestAnalysisCacheMapper mapper,
        IBitbucketTelemetryService telemetryService,
        string? cacheRootDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(telemetryService);

        _mapper = mapper;
        _telemetryService = telemetryService;
        _cacheRootDirectory = string.IsNullOrWhiteSpace(cacheRootDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "cache", "pull-request-analysis")
            : cacheRootDirectory.Trim();
    }

    /// <inheritdoc />
    public bool TryGet(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        string pullRequestFingerprint,
        string parametersFingerprint,
        out PullRequestAnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);
        ArgumentNullException.ThrowIfNull(pullRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pullRequestFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(parametersFingerprint);

        snapshot = null!;

        var cacheFilePath = GetCacheFilePath(
            workspace.Value,
            repoSlug.Value,
            pullRequestId.Value,
            pullRequestFingerprint,
            parametersFingerprint);
        if (!File.Exists(cacheFilePath))
        {
            _telemetryService.TrackAnalysisSnapshotCacheMiss();
            return false;
        }

        try
        {
            var json = File.ReadAllText(cacheFilePath);
            var document = JsonSerializer.Deserialize<PullRequestAnalysisCacheDocument>(json, _serializerOptions);
            if (document is null ||
                document.Version != CURRENT_VERSION ||
                document.Snapshot is null ||
                !_mapper.TryMap(document.Snapshot, out snapshot))
            {
                _telemetryService.TrackAnalysisSnapshotCacheMiss();
                return false;
            }

            _telemetryService.TrackAnalysisSnapshotCacheHit(snapshot);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            _telemetryService.TrackAnalysisSnapshotCacheMiss();
            return false;
        }
    }

    /// <inheritdoc />
    public void Store(
        Workspace workspace,
        RepoSlug repoSlug,
        PullRequestId pullRequestId,
        string pullRequestFingerprint,
        string parametersFingerprint,
        PullRequestAnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);
        ArgumentNullException.ThrowIfNull(pullRequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pullRequestFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(parametersFingerprint);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!IsValid(snapshot))
        {
            return;
        }

        var cacheFilePath = GetCacheFilePath(
            workspace.Value,
            repoSlug.Value,
            pullRequestId.Value,
            pullRequestFingerprint,
            parametersFingerprint);
        var cacheDirectory = Path.GetDirectoryName(cacheFilePath);
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            return;
        }

        string? tempFilePath = null;
        try
        {
            _ = Directory.CreateDirectory(cacheDirectory);

            var document = new PullRequestAnalysisCacheDocument
            {
                Version = CURRENT_VERSION,
                Snapshot = _mapper.Map(snapshot)
            };

            tempFilePath = $"{cacheFilePath}.{Guid.NewGuid():N}.tmp";
            var json = JsonSerializer.Serialize(document, _serializerOptions);
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, cacheFilePath, overwrite: true);
            _telemetryService.TrackAnalysisSnapshotCacheStore();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return;
        }
        finally
        {
            TryDeleteTemporaryFile(tempFilePath);
        }
    }

    private string GetCacheFilePath(
        string workspace,
        string repoSlug,
        int pullRequestId,
        string pullRequestFingerprint,
        string parametersFingerprint)
    {
        var workspaceSegment = GetHashSegment(workspace);
        var repositorySegment = GetHashSegment(repoSlug);
        var pullRequestSegment = pullRequestId.ToString(CultureInfo.InvariantCulture);
        var parametersSegment = GetHashSegment(parametersFingerprint);
        var fingerprintSegment = GetHashSegment(pullRequestFingerprint);

        return Path.Combine(
            _cacheRootDirectory,
            workspaceSegment,
            repositorySegment,
            pullRequestSegment,
            parametersSegment,
            $"{fingerprintSegment}.json");
    }

    private static string GetHashSegment(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static bool IsValid(PullRequestAnalysisSnapshot snapshot) =>
        snapshot.Activities.All(static activity => activity is not null) &&
        snapshot.CorrectionCommits.All(static commit => commit is not null) &&
        snapshot.CommitActivities.All(static activity => activity is not null);

    private static void TryDeleteTemporaryFile(string? tempFilePath)
    {
        if (string.IsNullOrWhiteSpace(tempFilePath) || !File.Exists(tempFilePath))
        {
            return;
        }
        try
        {
            File.Delete(tempFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return;
        }
    }

    private const int CURRENT_VERSION = 1;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _cacheRootDirectory;

    private readonly IPullRequestAnalysisCacheMapper _mapper;
    private readonly IBitbucketTelemetryService _telemetryService;
}

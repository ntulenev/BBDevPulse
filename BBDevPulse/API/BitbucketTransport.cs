using System.Net;
using System.Text.Json;

using BBDevPulse.Abstractions;

namespace BBDevPulse.API;

/// <summary>
/// Default Bitbucket HTTP transport.
/// </summary>
internal sealed class BitbucketTransport : IBitbucketTransport
{
    private readonly HttpClient _httpClient;
    private readonly IRetryPolicyHelper _retryPolicyHelper;
    private readonly IBitbucketTelemetryService _telemetryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTransport"/> class.
    /// </summary>
    /// <param name="httpClient">Configured HTTP client.</param>
    /// <param name="retryPolicyHelper">Retry policy helper.</param>
    /// <param name="telemetryService">Telemetry service.</param>
    public BitbucketTransport(
        HttpClient httpClient,
        IRetryPolicyHelper retryPolicyHelper,
        IBitbucketTelemetryService telemetryService)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(retryPolicyHelper);
        ArgumentNullException.ThrowIfNull(telemetryService);
        _httpClient = httpClient;
        _retryPolicyHelper = retryPolicyHelper;
        _telemetryService = telemetryService;
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(Uri url, CancellationToken cancellationToken)
    {
        return await _retryPolicyHelper.ExecuteAsync(
            async token =>
            {
                _telemetryService.TrackRequest(url);
                using var response = await _httpClient.GetAsync(url, token)
                    .ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var body = await response.Content.ReadAsStringAsync(token)
                        .ConfigureAwait(false);
                    throw new RetryableBitbucketException(response.StatusCode, body);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(token)
                        .ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"Bitbucket API request failed ({response.StatusCode}): {body}");
                }

                using var stream = await response.Content.ReadAsStreamAsync(token)
                    .ConfigureAwait(false);

                var result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, token)
                    .ConfigureAwait(false);

                return result == null ? throw new InvalidOperationException("Bitbucket API response was empty.") : result;
            },
            static ex => ex is RetryableBitbucketException,
            cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

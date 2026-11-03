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
        ArgumentNullException.ThrowIfNull(url);
        var requestUri = ResolveRequestUri(url);
        return await _retryPolicyHelper.ExecuteAsync(
            async token =>
            {
                _telemetryService.TrackRequest(requestUri);
                using var response = await _httpClient.GetAsync(requestUri, token)
                    .ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.TooManyRequests or
                    HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                {
                    var body = await response.Content.ReadAsStringAsync(token)
                        .ConfigureAwait(false);
                    var retryAfter = response.Headers.TryGetValues("Retry-After", out var values)
                        ? values.FirstOrDefault()
                        : null;
                    throw new RetryableBitbucketException(response.StatusCode, body, retryAfter);
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

    private Uri ResolveRequestUri(Uri url)
    {
        var baseAddress = _httpClient.BaseAddress;
        if (baseAddress is null)
        {
            if (!url.IsAbsoluteUri)
            {
                throw new InvalidOperationException("Bitbucket API base address is missing.");
            }

            return url;
        }

        Uri resolved;
        try
        {
            resolved = new Uri(baseAddress, url);
        }
        catch (UriFormatException exception)
        {
            throw new InvalidOperationException("Invalid Bitbucket pagination URI.", exception);
        }

        if (resolved.Scheme != baseAddress.Scheme ||
            !string.Equals(resolved.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase) ||
            resolved.Port != baseAddress.Port ||
            !string.IsNullOrEmpty(resolved.UserInfo))
        {
            throw new InvalidOperationException("Bitbucket pagination URI has a different API origin.");
        }

        return resolved;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

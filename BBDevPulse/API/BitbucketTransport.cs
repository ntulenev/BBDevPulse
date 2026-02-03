using System.Text.Json;

using BBDevPulse.Abstractions;

namespace BBDevPulse.API;

/// <summary>
/// Default Bitbucket HTTP transport.
/// </summary>
internal sealed class BitbucketTransport : IBitbucketTransport
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTransport"/> class.
    /// </summary>
    /// <param name="httpClient">Configured HTTP client.</param>
    public BitbucketTransport(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(Uri url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Bitbucket API request failed ({response.StatusCode}): {body}");
        }

#pragma warning disable CA2007 // Not needed here
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
        var result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result == null)
        {
            throw new InvalidOperationException("Bitbucket API response was empty.");
        }

        return result;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

namespace BBDevPulse.Abstractions;

/// <summary>
/// Wraps Bitbucket HTTP transport.
/// </summary>
internal interface IBitbucketTransport
{
    /// <summary>
    /// Sends a GET request and deserializes JSON into the requested type.
    /// </summary>
    Task<T> GetAsync<T>(Uri url, CancellationToken cancellationToken);
}

using System.Net;

namespace BBDevPulse.API;

/// <summary>
/// Represents a retryable Bitbucket API failure.
/// </summary>
internal sealed class RetryableBitbucketException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryableBitbucketException"/> class.
    /// </summary>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="body">Response body.</param>
    public RetryableBitbucketException(HttpStatusCode statusCode, string body)
        : base($"Bitbucket API request failed ({statusCode}): {body}")
    {
    }
}

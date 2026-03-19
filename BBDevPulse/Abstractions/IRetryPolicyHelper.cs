namespace BBDevPulse.Abstractions;

/// <summary>
/// Executes operations with a retry policy.
/// </summary>
internal interface IRetryPolicyHelper
{
    /// <summary>
    /// Executes the provided operation and retries when the predicate matches the thrown exception.
    /// </summary>
    /// <typeparam name="T">Operation result type.</typeparam>
    /// <param name="operation">Operation to execute.</param>
    /// <param name="shouldRetry">Predicate indicating whether the exception is retryable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken);
}

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;

using Microsoft.Extensions.Options;

namespace BBDevPulse.API;

/// <summary>
/// Default retry policy helper for transient transport failures.
/// </summary>
internal sealed class RetryPolicyHelper : IRetryPolicyHelper
{
    private readonly BitbucketOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicyHelper"/> class.
    /// </summary>
    /// <param name="options">Bitbucket options.</param>
    public RetryPolicyHelper(IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        var retryAttempt = 0;
        while (true)
        {
            await WaitForGlobalCooldownAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                       shouldRetry(ex) &&
                                       retryAttempt < _options.MaxRetries)
            {
                retryAttempt++;
                ExtendGlobalCooldown(GetDelay(retryAttempt));
            }
        }
    }

    internal TimeSpan GetDelay(int retryAttempt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retryAttempt);

        var delaySeconds = 1 + ((retryAttempt - 1) * _options.RetryDelayStepSeconds);
        return TimeSpan.FromSeconds(System.Math.Clamp(delaySeconds, 1, _options.MaxRetryDelaySeconds));
    }

    private async Task WaitForGlobalCooldownAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan delay;
            lock (_sync)
            {
                delay = _retryNotBeforeUtc - DateTimeOffset.UtcNow;
            }

            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ExtendGlobalCooldown(TimeSpan delay)
    {
        var retryNotBeforeUtc = DateTimeOffset.UtcNow.Add(delay);
        lock (_sync)
        {
            if (retryNotBeforeUtc > _retryNotBeforeUtc)
            {
                _retryNotBeforeUtc = retryNotBeforeUtc;
            }
        }
    }

    private readonly Lock _sync = new();
    private DateTimeOffset _retryNotBeforeUtc = DateTimeOffset.MinValue;
}

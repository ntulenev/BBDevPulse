using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;

using System.Globalization;

using Microsoft.Extensions.Options;

namespace BBDevPulse.API;

/// <summary>
/// Default retry policy helper for transient transport failures.
/// </summary>
internal sealed class RetryPolicyHelper : IRetryPolicyHelper
{
    private readonly BitbucketOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicyHelper"/> class.
    /// </summary>
    /// <param name="options">Bitbucket options.</param>
    /// <param name="timeProvider">Optional clock for scheduling retries.</param>
    public RetryPolicyHelper(IOptions<BitbucketOptions> options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
                var delay = GetDelay(retryAttempt);
                if (ex is RetryableBitbucketException retryable &&
                    TryGetRetryAfterDelay(retryable.RetryAfter, out var serverDelay))
                {
                    if (serverDelay > TimeSpan.FromSeconds(_options.MaxRetryDelaySeconds))
                    {
                        throw new InvalidOperationException(
                            $"Bitbucket API request failed ({retryable.StatusCode}): Retry-After exceeds the allowed wait.", ex);
                    }

                    delay = delay > serverDelay ? delay : serverDelay;
                }

                ExtendGlobalCooldown(delay);
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
                delay = _retryNotBeforeUtc - _timeProvider.GetUtcNow();
            }

            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ExtendGlobalCooldown(TimeSpan delay)
    {
        var retryNotBeforeUtc = _timeProvider.GetUtcNow().Add(delay);
        lock (_sync)
        {
            if (retryNotBeforeUtc > _retryNotBeforeUtc)
            {
                _retryNotBeforeUtc = retryNotBeforeUtc;
            }
        }
    }

    private bool TryGetRetryAfterDelay(string? value, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            delay = seconds >= TimeSpan.MaxValue.TotalSeconds
                ? TimeSpan.MaxValue
                : TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var deadline))
        {
            delay = deadline - _timeProvider.GetUtcNow();
            return delay > TimeSpan.Zero;
        }

        return false;
    }

    private readonly Lock _sync = new();
    private DateTimeOffset _retryNotBeforeUtc = DateTimeOffset.MinValue;
}

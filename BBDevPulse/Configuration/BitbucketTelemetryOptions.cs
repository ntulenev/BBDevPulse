namespace BBDevPulse.Configuration;

/// <summary>
/// Bitbucket telemetry configuration.
/// </summary>
public sealed class BitbucketTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether telemetry is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;
}

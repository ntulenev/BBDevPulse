using System.Text.Json;

namespace BBDevPulse.Models;

/// <summary>
/// Pull request activity domain model.
/// </summary>
public sealed class PullRequestActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestActivity"/> class.
    /// </summary>
    /// <param name="payload">Raw activity payload.</param>
    public PullRequestActivity(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("Activity payload must be defined.", nameof(payload));
        }

        Payload = payload;
    }

    /// <summary>
    /// Raw activity payload.
    /// </summary>
    public JsonElement Payload { get; }
}

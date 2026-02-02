using System.Globalization;

namespace BBDevPulse.Models;

/// <summary>
/// Pull request identifier domain model.
/// </summary>
public sealed class PullRequestId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestId"/> class.
    /// </summary>
    /// <param name="value">Pull request identifier value.</param>
    public PullRequestId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Pull request id must be greater than 0.");
        }

        Value = value;
    }

    /// <summary>
    /// Pull request identifier value.
    /// </summary>
    public int Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

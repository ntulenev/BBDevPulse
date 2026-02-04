namespace BBDevPulse.Models;

/// <summary>
/// Holds mutable state while analyzing pull request activities.
/// </summary>
internal sealed class ActivityAnalysisState
{
    public ActivityAnalysisState(
        DateTimeOffset createdOn,
        DeveloperIdentity? authorIdentity,
        bool shouldCalculateTtfr)
    {
        CreatedOn = createdOn;
        LastActivity = createdOn;
        AuthorIdentity = authorIdentity;
        ShouldCalculateTtfr = shouldCalculateTtfr;
    }

    public DateTimeOffset CreatedOn { get; }

    public DateTimeOffset LastActivity { get; set; }

    public DateTimeOffset? MergedOnFromActivity { get; set; }

    public DateTimeOffset? FirstReactionOn { get; set; }

    public DeveloperIdentity? AuthorIdentity { get; }

    public bool ShouldCalculateTtfr { get; }

    public Dictionary<string, DeveloperIdentity> Participants { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> CommentCounts { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ApprovalCounts { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int TotalComments { get; set; }

    public void AddParticipant(DeveloperIdentity identity)
    {
        var key = identity.ToKey();
        if (!Participants.ContainsKey(key))
        {
            Participants[key] = identity;
        }
    }
}

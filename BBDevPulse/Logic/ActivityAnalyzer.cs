using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.Logic;

/// <summary>
/// Default activity analyzer.
/// </summary>
internal sealed class ActivityAnalyzer : IActivityAnalyzer
{
    /// <inheritdoc />
    public void Analyze(ActivityAnalysisState analysis, PullRequestActivity activity, DateTimeOffset filterDate)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(activity);

        var lastActivity = analysis.LastActivity;
        _ = activity.TryUpdateLastActivity(ref lastActivity);
        analysis.LastActivity = lastActivity;

        var mergedOnFromActivity = analysis.MergedOnFromActivity;
        _ = activity.TryUpdateMergedOn(ref mergedOnFromActivity);
        analysis.MergedOnFromActivity = mergedOnFromActivity;

        if (!activity.TryGetActor(out var activityUser))
        {
            return;
        }

        AddParticipant(analysis.Participants, activityUser);

        if (activity.Comment is not null)
        {
            analysis.TotalComments++;
            if (activity.Comment.IsOnOrAfter(filterDate))
            {
                var commentUser = activity.Comment.User;
                var commentKey = commentUser.ToKey();
                analysis.CommentCounts[commentKey] =
                    analysis.CommentCounts.GetValueOrDefault(commentKey) + 1;
                AddParticipant(analysis.Participants, commentUser);
            }

            if (analysis.ShouldCalculateTtfr &&
                activity.Comment.IsOnOrAfter(analysis.CreatedOn) &&
                activity.Comment.IsByDifferentDeveloper(analysis.AuthorIdentity))
            {
                var firstReactionOn = analysis.FirstReactionOn;
                _ = activity.Comment.TryUpdateFirstReaction(ref firstReactionOn);
                analysis.FirstReactionOn = firstReactionOn;
            }
        }

        if (activity.Approval is not null)
        {
            if (activity.Approval.IsOnOrAfter(filterDate))
            {
                var approvalUser = activity.Approval.User;
                var approvalKey = approvalUser.ToKey();
                analysis.ApprovalCounts[approvalKey] =
                    analysis.ApprovalCounts.GetValueOrDefault(approvalKey) + 1;
                AddParticipant(analysis.Participants, approvalUser);
            }

            if (analysis.ShouldCalculateTtfr &&
                activity.Approval.IsOnOrAfter(analysis.CreatedOn) &&
                activity.Approval.IsByDifferentDeveloper(analysis.AuthorIdentity))
            {
                var firstReactionOn = analysis.FirstReactionOn;
                _ = activity.Approval.TryUpdateFirstReaction(ref firstReactionOn);
                analysis.FirstReactionOn = firstReactionOn;
            }
        }
    }

    private static void AddParticipant(
        Dictionary<string, DeveloperIdentity> participants,
        DeveloperIdentity identity)
    {
        var key = identity.ToKey();
        if (!participants.ContainsKey(key))
        {
            participants[key] = identity;
        }
    }
}

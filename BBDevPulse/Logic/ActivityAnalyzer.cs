using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.Logic;

/// <summary>
/// Default activity analyzer.
/// </summary>
internal sealed class ActivityAnalyzer : IActivityAnalyzer
{
    /// <inheritdoc />
    public void Analyze(ActivityAnalysisState analysis, PullRequestActivity activity, ReportParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.IsInRange(activity.ActivityDate))
        {
            analysis.HasActivityInRange = true;
        }

        if (parameters.IsInRange(activity.ActivityDate))
        {
            var lastActivity = analysis.LastActivity;
            _ = activity.TryUpdateLastActivity(ref lastActivity);
            analysis.LastActivity = lastActivity;
        }

        if (parameters.IsInRange(activity.MergeDate))
        {
            var mergedOnFromActivity = analysis.MergedOnFromActivity;
            _ = activity.TryUpdateMergedOn(ref mergedOnFromActivity);
            analysis.MergedOnFromActivity = mergedOnFromActivity;
        }

        if (!activity.TryGetActor(out var activityUser))
        {
            return;
        }

        analysis.AddParticipant(activityUser);

        if (activity.Comment is not null)
        {
            if (parameters.IsInRange(activity.Comment.Date))
            {
                analysis.TotalComments++;
                var commentUser = activity.Comment.User;
                var commentKey = commentUser.ToKey();
                analysis.CommentCounts[commentKey] =
                    analysis.CommentCounts.GetValueOrDefault(commentKey) + 1;
                analysis.AddParticipant(commentUser);
            }

            if (analysis.ShouldCalculateTtfr &&
                parameters.IsInRange(activity.Comment.Date) &&
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
            if (parameters.IsInRange(activity.Approval.Date))
            {
                var approvalUser = activity.Approval.User;
                var approvalKey = approvalUser.ToKey();
                analysis.ApprovalCounts[approvalKey] =
                    analysis.ApprovalCounts.GetValueOrDefault(approvalKey) + 1;
                analysis.AddParticipant(approvalUser);
            }

            if (analysis.ShouldCalculateTtfr &&
                parameters.IsInRange(activity.Approval.Date) &&
                activity.Approval.IsOnOrAfter(analysis.CreatedOn) &&
                activity.Approval.IsByDifferentDeveloper(analysis.AuthorIdentity))
            {
                var firstReactionOn = analysis.FirstReactionOn;
                _ = activity.Approval.TryUpdateFirstReaction(ref firstReactionOn);
                analysis.FirstReactionOn = firstReactionOn;
            }
        }
    }

}

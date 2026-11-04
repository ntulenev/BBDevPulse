using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.Logic;

// Processes an already-loaded snapshot without accessing the network or cache.
internal static class PullRequestActivityAnalysis
{
    public static Result Analyze(
        PullRequest pr,
        PullRequestAnalysisSnapshot snapshot,
        ReportParameters parameters,
        DeveloperIdentity? authorIdentity,
        Func<DeveloperIdentity, bool> isDeveloperIncluded,
        IActivityAnalyzer activityAnalyzer)
    {
        var collectDeveloperDetails = parameters.ShowAllDetailsForDevelopers;
        var observedIdentities = new List<DeveloperIdentity>();
        var analysis = new ActivityAnalysisState(
            pr.CreatedOn,
            authorIdentity,
            pr.IsCreatedInRange(parameters));
        var detailedComments = collectDeveloperDetails
            ? new List<(DeveloperIdentity User, DateTimeOffset Date)>()
            : null;
        var detailedApprovals = collectDeveloperDetails
            ? new List<(DeveloperIdentity User, DateTimeOffset Date)>()
            : null;

        // A stable order also chooses a reproducible display name when one UUID has aliases.
        var orderedActivities = snapshot.Activities
            .OrderBy(static activity => activity.ActivityDate ?? DateTimeOffset.MaxValue)
            .ThenBy(static activity => activity.MergeDate ?? DateTimeOffset.MaxValue)
            .ThenBy(static activity => activity.Actor?.Uuid?.Value, StringComparer.Ordinal)
            .ThenBy(static activity => activity.Actor?.DisplayName.Value, StringComparer.Ordinal)
            .ThenBy(static activity => activity.Comment?.Date ?? DateTimeOffset.MaxValue)
            .ThenBy(static activity => activity.Comment?.User.Uuid?.Value, StringComparer.Ordinal)
            .ThenBy(static activity => activity.Comment?.User.DisplayName.Value, StringComparer.Ordinal)
            .ThenBy(static activity => activity.Approval?.Date ?? DateTimeOffset.MaxValue)
            .ThenBy(static activity => activity.Approval?.User.Uuid?.Value, StringComparer.Ordinal)
            .ThenBy(static activity => activity.Approval?.User.DisplayName.Value, StringComparer.Ordinal);
        foreach (var activity in orderedActivities)
        {
            if (activity.Actor.HasValue)
            {
                observedIdentities.Add(activity.Actor.Value);
            }
            if (activity.Comment is not null)
            {
                observedIdentities.Add(activity.Comment.User);
            }
            if (activity.Approval is not null)
            {
                observedIdentities.Add(activity.Approval.User);
            }
            if ((activity.Comment is not null && activity.Comment.User.Uuid is null) ||
                (activity.Approval is not null && activity.Approval.User.Uuid is null) ||
                (activity.Actor.HasValue && activity.Actor.Value.Uuid is null &&
                 activity.Comment is null && activity.Approval is null))
            {
                continue;
            }
            if (parameters.HasTeamFilter &&
                parameters.IsInRange(activity.ActivityDate) &&
                HasIncludedTeamActivity(activity, isDeveloperIncluded))
            {
                analysis.HasIncludedTeamActivity = true;
            }

            activityAnalyzer.Analyze(analysis, activity, parameters);

            if (collectDeveloperDetails)
            {
                if (activity.Comment is not null && activity.Comment.User.Uuid is not null && parameters.IsInRange(activity.Comment.Date))
                {
                    detailedComments!.Add((activity.Comment.User, activity.Comment.Date));
                }

                if (activity.Approval is not null && activity.Approval.User.Uuid is not null && parameters.IsInRange(activity.Approval.Date))
                {
                    detailedApprovals!.Add((activity.Approval.User, activity.Approval.Date));
                }
            }
        }

        return new Result(analysis, observedIdentities, detailedComments, detailedApprovals);
    }

    private static bool HasIncludedTeamActivity(PullRequestActivity activity, Func<DeveloperIdentity, bool> isDeveloperIncluded)
    {
        if (activity.Actor.HasValue && isDeveloperIncluded(activity.Actor.Value))
        {
            return true;
        }

        if (activity.Comment is not null && isDeveloperIncluded(activity.Comment.User))
        {
            return true;
        }

        return activity.Approval is not null && isDeveloperIncluded(activity.Approval.User);
    }

    internal sealed record Result(
        ActivityAnalysisState Analysis,
        List<DeveloperIdentity> ObservedIdentities,
        List<(DeveloperIdentity User, DateTimeOffset Date)>? DetailedComments,
        List<(DeveloperIdentity User, DateTimeOffset Date)>? DetailedApprovals);
}

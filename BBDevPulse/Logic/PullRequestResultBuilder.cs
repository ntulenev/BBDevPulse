using BBDevPulse.Models;
using BBDevPulse.Abstractions;

namespace BBDevPulse.Logic;

// Builds a complete contribution from loaded data without network or cache access.
internal static class PullRequestResultBuilder
{
    public enum Selection { Included, MissingAuthor, OutOfPeriod, OutsideTeam }

    internal sealed record Evaluation(
        Selection Selection, DeveloperIdentity? Author, bool IncludeAuthor,
        IReadOnlyList<DeveloperIdentity> Observed, PullRequestActivityAnalysis.Result? Activity);

    public static Evaluation Evaluate(PullRequest pr, ReportParameters parameters,
        PullRequestAnalysisSnapshot snapshot, Func<DeveloperIdentity, bool> isIncluded,
        IActivityAnalyzer activityAnalyzer)
    {
        var author = pr.Author?.ToDeveloperIdentity();
        var observed = new List<DeveloperIdentity> { author ?? new DeveloperIdentity(null, new DisplayName("unknown")) };
        if (!author.HasValue || author.Value.Uuid is null)
            return new Evaluation(Selection.MissingAuthor, author, false, observed, null);

        var includeAuthor = !parameters.HasTeamFilter || isIncluded(author.Value);
        var activity = PullRequestActivityAnalysis.Analyze(pr, snapshot, parameters, author, isIncluded, activityAnalyzer);
        observed.AddRange(activity.ObservedIdentities);
        var selection = !(parameters.IsInRange(pr.CreatedOn) || activity.Analysis.HasActivityInRange)
            ? Selection.OutOfPeriod
            : includeAuthor || (parameters.HasTeamFilter && activity.Analysis.HasIncludedTeamActivity)
                ? Selection.Included : Selection.OutsideTeam;
        return new Evaluation(selection, author, includeAuthor, observed, activity);
    }

    public static PullRequestAnalysisResult Complete(Repository repo, PullRequest pr, ReportParameters parameters,
        PullRequestAnalysisSnapshot snapshot, Evaluation evaluation)
    {
        if (evaluation.Selection != Selection.Included)
            return PullRequestAnalysisResult.Skipped(parameters.Workspace, repo.Slug, pr.Id, evaluation.Observed);
        return Create(repo, pr, parameters, snapshot, evaluation.Observed, evaluation.Author,
            evaluation.IncludeAuthor, evaluation.Activity!);
    }

    public static PullRequestAnalysisResult Analyze(Repository repo, PullRequest pr, ReportParameters parameters,
        PullRequestAnalysisSnapshot snapshot, Func<DeveloperIdentity, bool> isIncluded, IActivityAnalyzer activityAnalyzer) =>
        Complete(repo, pr, parameters, snapshot, Evaluate(pr, parameters, snapshot, isIncluded, activityAnalyzer));

    private static PullRequestAnalysisResult Create(
        Repository repo, PullRequest pr, ReportParameters parameters,
        PullRequestAnalysisSnapshot snapshot, IReadOnlyList<DeveloperIdentity> observedIdentities,
        DeveloperIdentity? authorIdentity, bool includeAuthoredPullRequest,
        PullRequestActivityAnalysis.Result activityResult)
    {
        var workspace = parameters.Workspace;
        var collectDeveloperDetails = parameters.ShowAllDetailsForDevelopers;
        var analysis = activityResult.Analysis;
        var detailedComments = activityResult.DetailedComments;
        var detailedApprovals = activityResult.DetailedApprovals;
        if (authorIdentity.HasValue)
        {
            analysis.Participants[authorIdentity.Value.ToKey()] = authorIdentity.Value;
        }

        var mergedOnResolved = analysis.MergedOnFromActivity ?? pr.MergedOn;
        if (!parameters.IsInRange(mergedOnResolved))
        {
            mergedOnResolved = null;
        }

        var rejectedOn = pr.ResolveRejectedOn();
        if (!parameters.IsInRange(rejectedOn))
        {
            rejectedOn = null;
        }

        var correctionCommits = snapshot.CorrectionCommits;
        IReadOnlyList<DeveloperCommitActivity> correctionCommitActivities = [];
        var sizeSummary = snapshot.SizeSummary;
        if (collectDeveloperDetails && authorIdentity.HasValue && includeAuthoredPullRequest && snapshot.CommitActivities.Count > 0)
        {
            correctionCommitActivities = snapshot.CommitActivities;
        }

        var corrections = correctionCommits.Count;
        var repositoryName = string.IsNullOrWhiteSpace(repo.Name.Value) ? repo.Slug.Value : repo.Name.Value;
        var pullRequestAuthor = pr.Author?.DisplayName.Value ?? "unknown";
        var reportEntry = new PullRequestReport(
                repositoryName,
                repo.Slug.Value,
                pullRequestAuthor,
                pr.Destination?.Branch?.Name ?? "-",
                pr.CreatedOn,
                analysis.LastActivity,
                mergedOnResolved,
                rejectedOn,
                pr.State,
                pr.Id,
                analysis.TotalComments,
                corrections,
                analysis.FirstReactionOn,
                sizeSummary.FilesChanged,
                sizeSummary.LinesAdded,
                sizeSummary.LinesRemoved,
                isActivityOnlyMatch: !includeAuthoredPullRequest,
                hasSizeData: snapshot.HasSize,
                hasCorrectionsData: snapshot.HasCommits,
                hasLineSizeData: snapshot.HasSize && snapshot.SizeSummary.HasLineData);

        return new PullRequestAnalysisResult(
            workspace, repo, pr, observedIdentities, authorIdentity, includeAuthoredPullRequest,
            reportEntry, mergedOnResolved, corrections, snapshot.HasCommits, analysis,
            correctionCommitActivities, detailedComments, detailedApprovals);
    }
}

using BBDevPulse.Logic;
using BBDevPulse.Models;

using FluentAssertions;

namespace BBDevPulse.Tests.Logic;

public sealed class PullRequestActivityAnalysisTests
{
    [Fact]
    public void AnalyzeSnapshotWithoutIoPreservesMissingIdentityDiagnostics()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var author = new User(new DisplayName("Alice"), new UserUuid("{a}"));
        var reviewer = new DeveloperIdentity(new UserUuid("{b}"), new DisplayName("Alice"));
        var missing = new DeveloperIdentity(null, new DisplayName("Unknown"));
        var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null, author, null);
        var snapshot = new PullRequestAnalysisSnapshot(
            [new(date.AddHours(1), null, reviewer, new ActivityComment(reviewer, date.AddHours(1)), null),
             new(date.AddHours(2), null, missing, new ActivityComment(missing, date.AddHours(2)), null)],
            [], PullRequestSizeSummary.Empty, []);
        var parameters = new ReportParameters(date.AddDays(-1), new Workspace("workspace"),
            new RepoNameFilter(string.Empty), [], RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly, []);

        var result = PullRequestActivityAnalysis.Analyze(pr, snapshot, parameters,
            author.ToDeveloperIdentity(), _ => true, new ActivityAnalyzer());

        result.Analysis.TotalComments.Should().Be(1);
        result.Analysis.CommentCounts[reviewer.ToKey()].Should().Be(1);
        result.ObservedIdentities.Should().Contain(missing);
        result.Analysis.FirstReactionOn.Should().Be(date.AddHours(1));
    }
    [Fact]
    public void CompletePullRequestAnalysisUsesOnlyProvidedSnapshot()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var author = new User(new DisplayName("Author"), new UserUuid("{a}"));
        var reviewer = new DeveloperIdentity(new UserUuid("{b}"), new DisplayName("Reviewer"));
        var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null, author, null);
        var repo = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var snapshot = new PullRequestAnalysisSnapshot(
            [new(date.AddHours(1), null, reviewer, new ActivityComment(reviewer, date.AddHours(1)), null)],
            [], new PullRequestSizeSummary(2, 3, 4), [], hasEnrichment: true);
        var parameters = new ReportParameters(date.AddDays(-1), new Workspace("workspace"),
            new RepoNameFilter(string.Empty), [], RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly, []);

        var result = PullRequestResultBuilder.Analyze(repo, pr, parameters, snapshot, _ => true, new ActivityAnalyzer());

        result.Report.Should().NotBeNull();
        result.Report!.Comments.Should().Be(1);
        result.Report.FilesChanged.Should().Be(2);
        result.CommentCounts[reviewer.ToKey()].Should().Be(1);
        result.HasCommits.Should().BeTrue();
    }
    [Fact]
    public void ReorderingActivitiesWithAliasesKeepsCompleteResultStable()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var author = new User(new DisplayName("Author"), new UserUuid("{a}"));
        var first = new DeveloperIdentity(new UserUuid("{b}"), new DisplayName("Alex"));
        var second = new DeveloperIdentity(new UserUuid("{b}"), new DisplayName("Zoe"));
        var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null, author, null);
        var repo = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var activities = new[] {
            new PullRequestActivity(date.AddHours(1), null, second, new ActivityComment(second, date.AddHours(1)), null),
            new PullRequestActivity(date.AddHours(1), null, first, new ActivityComment(first, date.AddHours(1)), null)
        };
        var parameters = new ReportParameters(date.AddDays(-1), new Workspace("workspace"),
            new RepoNameFilter(string.Empty), [], RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly, []);
        PullRequestAnalysisResult Analyze(IEnumerable<PullRequestActivity> input) => PullRequestResultBuilder.Analyze(
            repo, pr, parameters, new PullRequestAnalysisSnapshot(input.ToArray(), [], PullRequestSizeSummary.Empty, []),
            _ => true, new ActivityAnalyzer());

        var forward = Analyze(activities);
        var reverse = Analyze(activities.Reverse());

        forward.Participants[first.ToKey()].DisplayName.Value.Should().Be("Alex");
        reverse.Participants.Should().BeEquivalentTo(forward.Participants);
        reverse.ObservedIdentities.Should().Equal(forward.ObservedIdentities);
        reverse.CommentCounts.Should().BeEquivalentTo(forward.CommentCounts);
        reverse.Report!.Comments.Should().Be(forward.Report!.Comments);
    }
}

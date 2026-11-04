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
}

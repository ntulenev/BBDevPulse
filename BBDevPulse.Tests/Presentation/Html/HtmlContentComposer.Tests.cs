using FluentAssertions;

using BBDevPulse.Math;
using BBDevPulse.Models;
using BBDevPulse.Presentation.Formatters;
using BBDevPulse.Presentation.Html;

namespace BBDevPulse.Tests.Presentation.Html;

public sealed class HtmlContentComposerTests
{
    [Fact(DisplayName = "Compose renders interactive report sections for populated data")]
    [Trait("Category", "Unit")]
    public void ComposeWhenDataExistsRendersExpectedSectionsAndTableControls()
    {
        // Arrange
        var composer = new HtmlContentComposer(new DateDiffFormatter(), new StatisticsCalculator());
        var reportData = CreateDetailedReportData();

        // Act
        var html = composer.Compose(reportData);

        // Assert
        html.Should().Contain("BBDevPulse HTML Report");
        html.Should().Contain("Pull Requests");
        html.Should().Contain("PR Throughput");
        html.Should().Contain("PRs per Developer");
        html.Should().Contain("Comments Stats");
        html.Should().Contain("PRs Rejected");
        html.Should().Contain("Worst PRs by Metric");
        html.Should().Contain("Developer Stats");
        html.Should().Contain("Developer Details");
        html.Should().Contain("data-table-panel");
        html.Should().Contain("data-sort-column=\"0\"");
        html.Should().Contain("Reset Filters");
        html.Should().Contain("<h2>Merge Time Stats</h2>");
        html.Should().NotContain("id=\"merge-time-stats\">\r\n  <div class=\"section-header\">\r\n    <h2>Merge Time Stats</h2>\r\n  </div>\r\n  <div class=\"table-panel\" data-table-panel>");
        html.Should().Contain("activity-only");
        html.Should().Contain("Orange rows indicate PRs authored outside the selected team");
        html.Should().Contain("pull-requests/200");
        html.Should().Contain("Follow-up Commits");
    }

    [Fact(DisplayName = "Compose renders empty-state text when report has no data")]
    [Trait("Category", "Unit")]
    public void ComposeWhenDataIsEmptyRendersEmptyStates()
    {
        // Arrange
        var composer = new HtmlContentComposer(new DateDiffFormatter(), new StatisticsCalculator());
        var reportData = new ReportData(CreateParameters());

        // Act
        var html = composer.Compose(reportData);

        // Assert
        html.Should().Contain("No pull requests in the report.");
        html.Should().Contain("PR Throughput");
        html.Should().Contain("PRs per Developer");
        html.Should().Contain("Comments Stats");
        html.Should().Contain("PRs Created");
        html.Should().Contain("PRs Merged");
        html.Should().Contain("PRs Rejected");
        html.Should().Contain("No data available in the report.");
        html.Should().Contain("No developer activity found in the report.");
    }

    private static ReportParameters CreateParameters() =>
        new(
            new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            repoNameList: [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            branchNameList: [],
            showDeveloperUuidInStats: true,
            showAllDetailsForDevelopers: true);

    private static ReportData CreateDetailedReportData()
    {
        var reportData = new ReportData(CreateParameters());
        var filterDate = reportData.Parameters.FilterDate;
        var report = new PullRequestReport(
            repository: "Repo One",
            repositorySlug: "repo-one",
            author: "Alice",
            targetBranch: "develop",
            createdOn: filterDate.AddDays(-1),
            lastActivity: filterDate.AddDays(2),
            mergedOn: filterDate.AddDays(3),
            rejectedOn: null,
            state: PullRequestState.Merged,
            id: new PullRequestId(200),
            comments: 2,
            corrections: 2,
            firstReactionOn: filterDate.AddHours(4),
            filesChanged: 4,
            linesAdded: 60,
            linesRemoved: 20,
            isActivityOnlyMatch: true);
        reportData.Reports.Add(report);
        var developer = reportData.GetOrAddDeveloper(new DeveloperIdentity(
            new UserUuid("{alice-1}"),
            new DisplayName("Alice")));
        developer.PrsOpenedSince = 1;
        developer.PrsMergedAfter = 1;
        developer.CommentsAfter = 1;
        developer.ApprovalsAfter = 1;
        developer.Corrections = 2;
        developer.AuthoredPullRequests.Add(report);
        developer.CommentActivities.Add(new DeveloperCommentActivity("Repo One", "repo-one", new PullRequestId(200), "Alice", filterDate.AddHours(5)));
        developer.ApprovalActivities.Add(new DeveloperApprovalActivity("Repo One", "repo-one", new PullRequestId(200), "Alice", filterDate.AddHours(6)));
        developer.CommitActivities.Add(new DeveloperCommitActivity(
            "Repo One",
            "repo-one",
            new PullRequestId(200),
            "abcdef1234567890",
            "Add follow-up commit size and message",
            filterDate.AddHours(7),
            new PullRequestSizeSummary(FilesChanged: 3, LinesAdded: 40, LinesRemoved: 10)));
        reportData.DeveloperStats[DeveloperKey.FromIdentity(new DeveloperIdentity(
            new UserUuid("{bob-1}"),
            new DisplayName("Bob")))] =
            new DeveloperStats(new DisplayName("Bob"), new UserUuid("{bob-1}"))
            {
                PrsOpenedSince = 3,
                PrsMergedAfter = 2
            };
        return reportData;
    }
}

using FluentAssertions;

using BBDevPulse.Math;
using BBDevPulse.Models;
using BBDevPulse.Presentation.Formatters;
using BBDevPulse.Presentation.Html;

namespace BBDevPulse.Tests.Presentation.Html;

public sealed class HtmlContentComposerTests
{
    [Fact]
    public void ComposeShowsEscapedCollisionWarningBeforePullRequests()
    {
        var data = new ReportData(CreateParameters());
        data.ObserveDeveloperIdentity(new DeveloperIdentity(new UserUuid("{1}"), new DisplayName("A < B")));
        data.ObserveDeveloperIdentity(new DeveloperIdentity(new UserUuid("{2}"), new DisplayName("A < B")));
        var html = new HtmlContentComposer(new DateDiffFormatter(), new StatisticsCalculator()).Compose(data);
        html.Should().Contain(ReportData.IdentityCollisionWarning).And.Contain("A &lt; B: {1}, {2}");
        html.IndexOf(ReportData.IdentityCollisionWarning, StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("<h2>Pull Requests</h2>", StringComparison.Ordinal));
    }
    [Fact]
    public void ComposeShowsMissingUuidWarning()
    {
        var data = new ReportData(CreateParameters());
        data.ObserveDeveloperIdentity(new DeveloperIdentity(null, new DisplayName("Alice")));
        var html = new HtmlContentComposer(new DateDiffFormatter(), new StatisticsCalculator()).Compose(data);
        html.Should().Contain(ReportData.MissingUserUuidWarning);
    }

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
        html.Should().Contain("Peer Comments Stats");
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
        html.Should().Contain("Peer Comments");
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

    [Fact]
    public void ComposeDeveloperDetailsUsesSelectedSizeModeAndMarksUnavailableSize()
    {
        var composer = new HtmlContentComposer(new DateDiffFormatter(), new StatisticsCalculator());
        var reportData = CreateDetailedReportData(PullRequestSizeMode.Files);
        var developer = reportData.DeveloperStats.Values.Single(stat => stat.DisplayName.Value == "Alice");
        var created = reportData.Parameters.FilterDate;
        developer.MutableAuthoredPullRequests.Add(new PullRequestReport(
            "Repo One", "repo-one", "Alice", "develop", created, created,
            null, null, PullRequestState.Open, new PullRequestId(201), 0,
            hasSizeData: false));

        var html = composer.Compose(reportData);
        var authoredStart = html.IndexOf("id=\"developer-1-authored\"", StringComparison.Ordinal);
        var authoredEnd = html.IndexOf("id=\"developer-1-comments\"", authoredStart, StringComparison.Ordinal);
        var authored = html[authoredStart..authoredEnd];

        authored.Should().Contain("Files");
        authored.Should().Contain("S (4)");
        authored.Should().Contain("Unavailable");
        authored.Should().NotContain("XS (80)");
    }

    private static ReportParameters CreateParameters(PullRequestSizeMode sizeMode = PullRequestSizeMode.Lines) =>
        new(
            new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            repoNameList: [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            branchNameList: [],
            pullRequestSizeMode: sizeMode,
            showDeveloperUuidInStats: true,
            showAllDetailsForDevelopers: true);

    private static ReportData CreateDetailedReportData(PullRequestSizeMode sizeMode = PullRequestSizeMode.Lines)
    {
        var reportData = new ReportData(CreateParameters(sizeMode));
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
        reportData.MutableReports.Add(report);
        var developer = reportData.GetOrAddDeveloper(new DeveloperIdentity(
            new UserUuid("{alice-1}"),
            new DisplayName("Alice")));
        developer.PrsOpenedSince = 1;
        developer.PrsMergedAfter = 1;
        developer.CommentsAfter = 1;
        developer.PeerCommentsAfter = 1;
        developer.ApprovalsAfter = 1;
        developer.Corrections = 2;
        developer.MutableAuthoredPullRequests.Add(report);
        developer.MutableCommentActivities.Add(new DeveloperCommentActivity("Repo One", "repo-one", new PullRequestId(200), "Alice", filterDate.AddHours(5)));
        developer.MutableApprovalActivities.Add(new DeveloperApprovalActivity("Repo One", "repo-one", new PullRequestId(200), "Alice", filterDate.AddHours(6)));
        developer.MutableCommitActivities.Add(new DeveloperCommitActivity(
            "Repo One",
            "repo-one",
            new PullRequestId(200),
            "abcdef1234567890",
            "Add follow-up commit size and message",
            filterDate.AddHours(7),
            new PullRequestSizeSummary(FilesChanged: 3, LinesAdded: 40, LinesRemoved: 10)));
        var bob = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("{bob-1}"), new DisplayName("Bob")));
        bob.PrsOpenedSince = 3;
        bob.PrsMergedAfter = 2;
        return reportData;
    }
}

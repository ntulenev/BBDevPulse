using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Logic;
using BBDevPulse.Models;

using Moq;

using System.Reflection;
using System.Runtime.CompilerServices;

namespace BBDevPulse.Tests.Logic;

public sealed class PullRequestAnalyzerTests
{
    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "Constructor throws when client is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenClientIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketClient client = null!;

        // Act
        Action act = () => _ = new PullRequestAnalyzer(client, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when activity analyzer is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenActivityAnalyzerIsNullThrowsArgumentNullException()
    {
        // Arrange
        IActivityAnalyzer activityAnalyzer = null!;

        // Act
        Action act = () => _ = new PullRequestAnalyzer(new Mock<IBitbucketClient>(MockBehavior.Strict).Object, activityAnalyzer);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnalyzeAsync throws when repository is null")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenRepositoryIsNullThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new PullRequestAnalyzer(new Mock<IBitbucketClient>(MockBehavior.Strict).Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);
        Repository repository = null!;
        var reportData = new ReportData(CreateParameters(DateTimeOffset.UtcNow));

        // Act
        Func<Task> act = async () => await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnalyzeAsync throws when report data is null")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new PullRequestAnalyzer(new Mock<IBitbucketClient>(MockBehavior.Strict).Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        ReportData reportData = null!;

        // Act
        Func<Task> act = async () => await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnalyzeAsync skips pull requests that do not match target branch filter")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenBranchDoesNotMatchSkipsPullRequest()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate, branchNameList: [new BranchName("main")]));
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var pullRequest = new PullRequest(
            new PullRequestId(1),
            PullRequestState.Open,
            closedOn: null,
            createdOn: filterDate.AddDays(1),
            updatedOn: null,
            mergedOn: null,
            author: null,
            destination: new PullRequestDestination(new PullRequestBranch("develop")));

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        var activityFetchCalls = 0;
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => activityFetchCalls++)
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));

        var analyzer = new PullRequestAnalyzer(client.Object, activityAnalyzer.Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().BeEmpty();
        activityFetchCalls.Should().Be(0);
    }

    [Fact(DisplayName = "AnalyzeAsync skips pull requests when both creation and last activity are before filter date")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenPullRequestDoesNotMatchDateFilterSkipsReportEntry()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate));
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var pullRequest = new PullRequest(
            new PullRequestId(1),
            PullRequestState.Open,
            closedOn: null,
            createdOn: filterDate.AddDays(-10),
            updatedOn: filterDate.AddDays(-9),
            mergedOn: null,
            author: new User(new DisplayName("Author"), new UserUuid("{author-1}")),
            destination: null);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        var analyzeCalls = 0;
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));
        activityAnalyzer.Setup(x => x.Analyze(
                It.IsAny<ActivityAnalysisState>(),
                It.IsAny<PullRequestActivity>(),
                It.IsAny<DateTimeOffset>()))
            .Callback(() => analyzeCalls++);

        var analyzer = new PullRequestAnalyzer(client.Object, activityAnalyzer.Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().BeEmpty();
        reportData.DeveloperStats.Should().BeEmpty();
        analyzeCalls.Should().Be(0);
    }

    [Fact(DisplayName = "AnalyzeAsync aggregates report and developer statistics for matching pull request")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenPullRequestMatchesBuildsReportAndAggregatesStats()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var parameters = CreateParameters(filterDate, branchNameList: [new BranchName("develop")]);
        var reportData = new ReportData(parameters);
        var repository = new Repository(new RepoName("Repo Name"), new RepoSlug("repo-slug"));
        var author = new User(new DisplayName("Alice"), new UserUuid("{alice-1}"));
        var pullRequest = new PullRequest(
            new PullRequestId(42),
            PullRequestState.Declined,
            closedOn: filterDate.AddDays(3),
            createdOn: filterDate.AddDays(1),
            updatedOn: filterDate.AddDays(2),
            mergedOn: null,
            author: author,
            destination: new PullRequestDestination(new PullRequestBranch("develop")));

        var activity = new PullRequestActivity(
            activityDate: filterDate.AddDays(2),
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        var reviewerIdentity = new DeveloperIdentity(new UserUuid("{reviewer-1}"), new DisplayName("Reviewer"));
        var pullRequestStopPredicateCalled = false;
        var activityStopPredicateCalled = false;
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns((Workspace _, RepoSlug _, Func<PullRequest, bool> shouldStop, CancellationToken _) =>
            {
                pullRequestStopPredicateCalled = true;
                var historicalPullRequest = new PullRequest(
                    new PullRequestId(999),
                    PullRequestState.Open,
                    closedOn: null,
                    createdOn: filterDate.AddDays(-30),
                    updatedOn: filterDate.AddDays(-29),
                    mergedOn: null,
                    author: null,
                    destination: null);
                _ = shouldStop(historicalPullRequest);
                return ToAsyncEnumerable([pullRequest]);
            });
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns((Workspace _, RepoSlug _, PullRequestId _, Func<PullRequestActivity, bool> shouldStop, CancellationToken _) =>
            {
                activityStopPredicateCalled = true;
                _ = shouldStop(new PullRequestActivity(
                    activityDate: filterDate.AddDays(-2),
                    mergeDate: null,
                    actor: null,
                    comment: null,
                    approval: null));
                return ToAsyncEnumerable([activity]);
            });

        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        activityAnalyzer.Setup(x => x.Analyze(
                It.IsAny<ActivityAnalysisState>(),
                activity,
                filterDate))
            .Callback<ActivityAnalysisState, PullRequestActivity, DateTimeOffset>((analysis, _, _) =>
            {
                analysis.LastActivity = filterDate.AddDays(4);
                analysis.MergedOnFromActivity = filterDate.AddDays(5);
                analysis.TotalComments = 6;
                analysis.FirstReactionOn = filterDate.AddHours(3);

                var reviewerKey = reviewerIdentity.ToKey();
                analysis.Participants[reviewerKey] = reviewerIdentity;
                analysis.CommentCounts[reviewerKey] = 3;
                analysis.CommentCounts["missing-comment-participant"] = 9;
                analysis.ApprovalCounts[reviewerKey] = 2;
                analysis.ApprovalCounts["missing-approval-participant"] = 7;
            });

        var analyzer = new PullRequestAnalyzer(client.Object, activityAnalyzer.Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().ContainSingle();
        var report = reportData.Reports[0];
        report.Repository.Should().Be("Repo Name");
        report.RepositorySlug.Should().Be("repo-slug");
        report.Author.Should().Be("Alice");
        report.TargetBranch.Should().Be("develop");
        report.Id.Value.Should().Be(42);
        report.MergedOn.Should().Be(filterDate.AddDays(5));
        report.RejectedOn.Should().Be(filterDate.AddDays(3));
        report.Comments.Should().Be(6);
        report.FirstReactionOn.Should().Be(filterDate.AddHours(3));

        var authorKey = new DeveloperKey(new UserUuid("{alice-1}"));
        reportData.DeveloperStats.Should().ContainKey(authorKey);
        reportData.DeveloperStats[authorKey].PrsOpenedSince.Should().Be(1);
        reportData.DeveloperStats[authorKey].PrsMergedAfter.Should().Be(1);

        var reviewerKeyForStats = new DeveloperKey(new UserUuid("{reviewer-1}"));
        reportData.DeveloperStats.Should().ContainKey(reviewerKeyForStats);
        reportData.DeveloperStats[reviewerKeyForStats].CommentsAfter.Should().Be(3);
        reportData.DeveloperStats[reviewerKeyForStats].ApprovalsAfter.Should().Be(2);
        pullRequestStopPredicateCalled.Should().BeTrue();
        activityStopPredicateCalled.Should().BeTrue();
    }

    [Fact(DisplayName = "AnalyzeAsync uses unknown author label and skips author stats when author is missing")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenAuthorIsMissingUsesUnknownAuthorAndNoAuthorStats()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate));
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var pullRequest = new PullRequest(
            new PullRequestId(3),
            PullRequestState.Open,
            closedOn: null,
            createdOn: filterDate.AddDays(1),
            updatedOn: null,
            mergedOn: null,
            author: null,
            destination: null);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));

        var analyzer = new PullRequestAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().ContainSingle();
        reportData.Reports[0].Author.Should().Be("unknown");
        reportData.DeveloperStats.Should().BeEmpty();
    }

    [Fact(DisplayName = "AnalyzeAsync uses repository slug when repository name is whitespace in invalid state")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenRepositoryNameIsWhitespaceUsesRepositorySlugInReport()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate));
        var repository = new Repository(CreateRepoNameWithoutValidation(" "), new RepoSlug("repo-slug"));
        var pullRequest = new PullRequest(
            new PullRequestId(4),
            PullRequestState.Open,
            closedOn: null,
            createdOn: filterDate.AddDays(1),
            updatedOn: null,
            mergedOn: null,
            author: null,
            destination: null);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));

        var analyzer = new PullRequestAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().ContainSingle();
        reportData.Reports[0].Repository.Should().Be("repo-slug");
    }

    [Fact(DisplayName = "AnalyzeAsync keeps older pull request when recent activity exists and does not increment merged counters when unresolved")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenCreatedBeforeFilterButRecentlyActiveAddsReportWithoutMergedCounter()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate));
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var author = new User(new DisplayName("Author"), new UserUuid("{author-1}"));
        var pullRequest = new PullRequest(
            new PullRequestId(5),
            PullRequestState.Open,
            closedOn: null,
            createdOn: filterDate.AddDays(-10),
            updatedOn: filterDate.AddDays(-9),
            mergedOn: null,
            author: author,
            destination: null);
        var activity = new PullRequestActivity(
            activityDate: filterDate.AddDays(1),
            mergeDate: null,
            actor: null,
            comment: null,
            approval: null);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([activity]));

        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        activityAnalyzer.Setup(x => x.Analyze(
                It.IsAny<ActivityAnalysisState>(),
                activity,
                filterDate))
            .Callback<ActivityAnalysisState, PullRequestActivity, DateTimeOffset>((analysis, _, _) =>
            {
                analysis.LastActivity = filterDate.AddDays(2);
                analysis.MergedOnFromActivity = null;
            });

        var analyzer = new PullRequestAnalyzer(client.Object, activityAnalyzer.Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().ContainSingle();
        var authorKey = new DeveloperKey(new UserUuid("{author-1}"));
        reportData.DeveloperStats.Should().NotContainKey(authorKey);
    }

    private static ReportParameters CreateParameters(
        DateTimeOffset filterDate,
        IReadOnlyList<BranchName>? branchNameList = null)
    {
        return new ReportParameters(
            filterDate,
            new Workspace("ws"),
            new RepoNameFilter(string.Empty),
            repoNameList: [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            branchNameList ?? []);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IReadOnlyList<T> values)
    {
        foreach (var value in values)
        {
            yield return value;
            await Task.Yield();
        }
    }

    private static RepoName CreateRepoNameWithoutValidation(string value)
    {
        var repoName = (RepoName)RuntimeHelpers.GetUninitializedObject(typeof(RepoName));
        var field = typeof(RepoName).GetField(
            "<Value>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(repoName, value);
        return repoName;
    }
}

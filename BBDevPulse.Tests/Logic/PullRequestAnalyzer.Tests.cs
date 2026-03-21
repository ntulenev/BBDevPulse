using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Logic;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;
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
        var options = CreateBitbucketOptions();

        // Act
        Action act = () => _ = new PullRequestAnalyzer(client, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object, options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when activity analyzer is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenActivityAnalyzerIsNullThrowsArgumentNullException()
    {
        // Arrange
        IActivityAnalyzer activityAnalyzer = null!;
        var options = CreateBitbucketOptions();

        // Act
        Action act = () => _ = new PullRequestAnalyzer(new Mock<IBitbucketClient>(MockBehavior.Strict).Object, activityAnalyzer, options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict).Object;
        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object;
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new PullRequestAnalyzer(client, activityAnalyzer, options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnalyzeAsync throws when repository is null")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenRepositoryIsNullThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = CreateAnalyzer(new Mock<IBitbucketClient>(MockBehavior.Strict).Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);
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
        var analyzer = CreateAnalyzer(new Mock<IBitbucketClient>(MockBehavior.Strict).Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);
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
        SetupPullRequestSize(client);
        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        var activityFetchCalls = 0;
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 1),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => activityFetchCalls++)
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));

        var analyzer = CreateAnalyzer(client.Object, activityAnalyzer.Object);

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
        SetupPullRequestSize(client);
        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        var analyzeCalls = 0;
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 1),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));
        activityAnalyzer.Setup(x => x.Analyze(
                It.Is<ActivityAnalysisState>(analysis => analysis != null),
                It.Is<PullRequestActivity>(activity => activity != null),
                It.Is<ReportParameters>(parameters => parameters.FilterDate == filterDate && !parameters.HasUpperBound)))
            .Callback(() => analyzeCalls++);

        var analyzer = CreateAnalyzer(client.Object, activityAnalyzer.Object);

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
        SetupPullRequestSize(client, new PullRequestSizeSummary(FilesChanged: 7, LinesAdded: 120, LinesRemoved: 30));
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
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
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 42),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
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
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 42),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([
                new PullRequestCommitInfo("commit-42-3", filterDate.AddDays(6), "Latest follow-up commit"),
                new PullRequestCommitInfo("commit-42-2", filterDate.AddDays(2), "Relevant fix"),
                new PullRequestCommitInfo("commit-42-1", filterDate.AddDays(1), "Initial commit")
            ]));

        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        activityAnalyzer.Setup(x => x.Analyze(
                It.Is<ActivityAnalysisState>(analysis =>
                    analysis.CreatedOn == pullRequest.CreatedOn &&
                    analysis.AuthorIdentity.HasValue &&
                    analysis.AuthorIdentity.Value.DisplayName.Value == "Alice"),
                activity,
                It.Is<ReportParameters>(value => value.FilterDate == filterDate && !value.HasUpperBound)))
            .Callback<ActivityAnalysisState, PullRequestActivity, ReportParameters>((analysis, _, _) =>
            {
                analysis.HasActivityInRange = true;
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

        var analyzer = CreateAnalyzer(client.Object, activityAnalyzer.Object);

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
        report.Corrections.Should().Be(2);
        report.FirstReactionOn.Should().Be(filterDate.AddHours(3));
        report.FilesChanged.Should().Be(7);
        report.LinesAdded.Should().Be(120);
        report.LinesRemoved.Should().Be(30);
        report.LineChurn.Should().Be(150);
        report.SizeTier.Should().Be("S");

        var authorKey = new DeveloperKey(new UserUuid("{alice-1}"));
        reportData.DeveloperStats.Should().ContainKey(authorKey);
        reportData.DeveloperStats[authorKey].PrsOpenedSince.Should().Be(1);
        reportData.DeveloperStats[authorKey].PrsMergedAfter.Should().Be(1);
        reportData.DeveloperStats[authorKey].Corrections.Should().Be(2);

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
        SetupPullRequestSize(client);
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 3),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 3),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestCommitInfo>([]));

        var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);

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
        SetupPullRequestSize(client);
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 4),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 4),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestCommitInfo>([]));

        var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);

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
        SetupPullRequestSize(client);
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 5),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([activity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 5),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([
                new PullRequestCommitInfo("commit-5-2", filterDate.AddDays(2), "Recent change"),
                new PullRequestCommitInfo("commit-5-1", filterDate.AddDays(1), "Another recent change"),
                new PullRequestCommitInfo("commit-5-0", filterDate.AddDays(-11), "Old change")
            ]));

        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        activityAnalyzer.Setup(x => x.Analyze(
                It.Is<ActivityAnalysisState>(analysis => analysis.CreatedOn == pullRequest.CreatedOn),
                activity,
                It.Is<ReportParameters>(value => value.FilterDate == filterDate && !value.HasUpperBound)))
            .Callback<ActivityAnalysisState, PullRequestActivity, ReportParameters>((analysis, _, _) =>
            {
                analysis.HasActivityInRange = true;
                analysis.LastActivity = filterDate.AddDays(2);
                analysis.MergedOnFromActivity = null;
            });

        var analyzer = CreateAnalyzer(client.Object, activityAnalyzer.Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().ContainSingle();
        reportData.Reports[0].Corrections.Should().Be(2);
        var authorKey = new DeveloperKey(new UserUuid("{author-1}"));
        reportData.DeveloperStats.Should().ContainKey(authorKey);
        reportData.DeveloperStats[authorKey].PrsOpenedSince.Should().Be(0);
        reportData.DeveloperStats[authorKey].PrsMergedAfter.Should().Be(0);
        reportData.DeveloperStats[authorKey].Corrections.Should().Be(2);
    }

    [Fact(DisplayName = "AnalyzeAsync ignores correction commit fetch failures and keeps report generation running")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenCorrectionCommitFetchFailsKeepsReportGenerationRunning()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate));
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var author = new User(new DisplayName("Author"), new UserUuid("{author-1}"));
        var pullRequest = new PullRequest(
            new PullRequestId(6),
            PullRequestState.Merged,
            closedOn: filterDate.AddDays(3),
            createdOn: filterDate.AddDays(1),
            updatedOn: filterDate.AddDays(2),
            mergedOn: filterDate.AddDays(3),
            author: author,
            destination: new PullRequestDestination(new PullRequestBranch("develop")));

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        SetupPullRequestSize(client);
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 6),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 6),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Throws(new InvalidOperationException("Bitbucket API request failed (TooManyRequests): "));

        var analyzer = CreateAnalyzer(client.Object, new ActivityAnalyzer());

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().ContainSingle();
        reportData.Reports[0].Corrections.Should().Be(0);
        var authorKey = new DeveloperKey(new UserUuid("{author-1}"));
        reportData.DeveloperStats.Should().ContainKey(authorKey);
        reportData.DeveloperStats[authorKey].PrsOpenedSince.Should().Be(1);
        reportData.DeveloperStats[authorKey].PrsMergedAfter.Should().Be(1);
        reportData.DeveloperStats[authorKey].Corrections.Should().Be(0);
    }

    [Fact(DisplayName = "AnalyzeAsync with team filter reports team authored PRs and external PRs with team activity")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenTeamFilterConfiguredIncludesExternalPrsWithTeamActivityButExcludesThemFromMetrics()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var parameters = CreateParameters(filterDate, teamFilter: "Core");
        var reportData = new ReportData(
            parameters,
            new Dictionary<DisplayName, PersonCsvRow>
            {
                [new DisplayName("Alice")] = new("Senior", "Core"),
                [new DisplayName("Reviewer")] = new("Senior", "Core"),
                [new DisplayName("Outsider")] = new("Senior", "Other"),
                [new DisplayName("External Reviewer")] = new("Senior", "Other")
            });
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var outsiderAuthor = new User(new DisplayName("Outsider"), new UserUuid("{outsider-1}"));
        var teamAuthor = new User(new DisplayName("Alice"), new UserUuid("{alice-1}"));
        var reviewerIdentity = new DeveloperIdentity(new UserUuid("{reviewer-1}"), new DisplayName("Reviewer"));
        var externalReviewerIdentity = new DeveloperIdentity(new UserUuid("{external-reviewer-1}"), new DisplayName("External Reviewer"));
        var outsiderPullRequest = new PullRequest(
            new PullRequestId(10),
            PullRequestState.Open,
            closedOn: null,
            createdOn: filterDate.AddDays(1),
            updatedOn: filterDate.AddDays(2),
            mergedOn: null,
            author: outsiderAuthor,
            destination: null);
        var teamPullRequest = new PullRequest(
            new PullRequestId(11),
            PullRequestState.Merged,
            closedOn: filterDate.AddDays(4),
            createdOn: filterDate.AddDays(2),
            updatedOn: filterDate.AddDays(3),
            mergedOn: filterDate.AddDays(4),
            author: teamAuthor,
            destination: new PullRequestDestination(new PullRequestBranch("develop")));
        var outsiderCommentActivity = new PullRequestActivity(
            activityDate: filterDate.AddDays(2),
            mergeDate: null,
            actor: reviewerIdentity,
            comment: new ActivityComment(reviewerIdentity, filterDate.AddDays(2)),
            approval: null);
        var outsiderApprovalActivity = new PullRequestActivity(
            activityDate: filterDate.AddDays(2).AddHours(1),
            mergeDate: null,
            actor: reviewerIdentity,
            comment: null,
            approval: new ActivityApproval(reviewerIdentity, filterDate.AddDays(2).AddHours(1)));
        var teamCommentActivity = new PullRequestActivity(
            activityDate: filterDate.AddDays(3),
            mergeDate: null,
            actor: externalReviewerIdentity,
            comment: new ActivityComment(externalReviewerIdentity, filterDate.AddDays(3)),
            approval: null);
        var teamMergeActivity = new PullRequestActivity(
            activityDate: filterDate.AddDays(3),
            mergeDate: filterDate.AddDays(4),
            actor: externalReviewerIdentity,
            comment: null,
            approval: null);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([outsiderPullRequest, teamPullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 10),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([outsiderCommentActivity, outsiderApprovalActivity]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 11),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([teamCommentActivity, teamMergeActivity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 10),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 11),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([
                new PullRequestCommitInfo("commit-11-3", filterDate.AddDays(5), "Newest team fix"),
                new PullRequestCommitInfo("commit-11-2", filterDate.AddDays(3), "In-range fix"),
                new PullRequestCommitInfo("commit-11-1", filterDate.AddDays(2), "Oldest in-range fix")
            ]));
        client.Setup(x => x.GetPullRequestSizeAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 10),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(PullRequestSizeSummary.Empty);
        client.Setup(x => x.GetPullRequestSizeAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 11),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PullRequestSizeSummary(FilesChanged: 4, LinesAdded: 100, LinesRemoved: 20));

        var analyzer = CreateAnalyzer(client.Object, new ActivityAnalyzer());

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().HaveCount(2);
        reportData.Reports.Should().ContainSingle(report => report.Author == "Outsider" && report.IsActivityOnlyMatch);
        reportData.Reports.Should().ContainSingle(report => report.Author == "Alice" && !report.IsActivityOnlyMatch);

        reportData.DeveloperStats.Should().ContainKey(new DeveloperKey(new UserUuid("{alice-1}")));
        reportData.DeveloperStats[new DeveloperKey(new UserUuid("{alice-1}"))].PrsOpenedSince.Should().Be(1);
        reportData.DeveloperStats[new DeveloperKey(new UserUuid("{alice-1}"))].PrsMergedAfter.Should().Be(1);
        reportData.DeveloperStats[new DeveloperKey(new UserUuid("{alice-1}"))].Corrections.Should().Be(2);

        reportData.DeveloperStats.Should().ContainKey(new DeveloperKey(new UserUuid("{reviewer-1}")));
        reportData.DeveloperStats[new DeveloperKey(new UserUuid("{reviewer-1}"))].CommentsAfter.Should().Be(1);
        reportData.DeveloperStats[new DeveloperKey(new UserUuid("{reviewer-1}"))].ApprovalsAfter.Should().Be(1);

        reportData.DeveloperStats.Should().NotContainKey(new DeveloperKey(new UserUuid("{outsider-1}")));
        reportData.DeveloperStats.Should().NotContainKey(new DeveloperKey(new UserUuid("{external-reviewer-1}")));
    }

    [Fact(DisplayName = "AnalyzeAsync excludes merge and correction metrics outside configured upper bound")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenUpperBoundConfiguredOnlyCountsInRangeMetrics()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var toDateExclusive = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate, toDateExclusive: toDateExclusive));
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var author = new User(new DisplayName("Alice"), new UserUuid("{alice-1}"));
        var pullRequest = new PullRequest(
            new PullRequestId(77),
            PullRequestState.Merged,
            closedOn: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
            createdOn: new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            updatedOn: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
            mergedOn: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
            author: author,
            destination: null);
        var activity = new PullRequestActivity(
            activityDate: new DateTimeOffset(2026, 2, 25, 0, 0, 0, TimeSpan.Zero),
            mergeDate: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
            actor: null,
            comment: null,
            approval: null);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        SetupPullRequestSize(client);
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 77),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([activity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 77),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([
                new PullRequestCommitInfo("commit-77-3", new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero), "Newest correction"),
                new PullRequestCommitInfo("commit-77-2", new DateTimeOffset(2026, 2, 25, 10, 0, 0, TimeSpan.Zero), "Middle correction"),
                new PullRequestCommitInfo("commit-77-1", new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero), "Initial change")
            ]));

        var analyzer = CreateAnalyzer(client.Object, new ActivityAnalyzer());

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().ContainSingle();
        var report = reportData.Reports[0];
        report.MergedOn.Should().BeNull();
        report.RejectedOn.Should().BeNull();
        report.Corrections.Should().Be(1);

        var authorKey = new DeveloperKey(new UserUuid("{alice-1}"));
        reportData.DeveloperStats.Should().ContainKey(authorKey);
        reportData.DeveloperStats[authorKey].PrsOpenedSince.Should().Be(1);
        reportData.DeveloperStats[authorKey].PrsMergedAfter.Should().Be(0);
        reportData.DeveloperStats[authorKey].Corrections.Should().Be(1);
    }

    [Fact(DisplayName = "AnalyzeAsync skips PR enrichment when pull request was created on upper bound")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenPullRequestCreatedAtUpperBoundSkipsHistoricalEnrichment()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var toDateExclusive = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate, toDateExclusive: toDateExclusive));
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var pullRequest = new PullRequest(
            new PullRequestId(78),
            PullRequestState.Open,
            closedOn: null,
            createdOn: toDateExclusive,
            updatedOn: toDateExclusive.AddDays(1),
            mergedOn: null,
            author: new User(new DisplayName("Alice"), new UserUuid("{alice-1}")),
            destination: null);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));

        var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().BeEmpty();
        reportData.DeveloperStats.Should().BeEmpty();
    }

    [Fact(DisplayName = "AnalyzeAsync collects detailed per-developer activities when configured")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenDetailedDeveloperOptionEnabledCollectsDetails()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var reportData = new ReportData(CreateParameters(filterDate, showAllDetailsForDevelopers: true));
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var author = new User(new DisplayName("Alice"), new UserUuid("{alice-1}"));
        var reviewerIdentity = new DeveloperIdentity(new UserUuid("{reviewer-1}"), new DisplayName("Reviewer"));
        var pullRequest = new PullRequest(
            new PullRequestId(88),
            PullRequestState.Merged,
            closedOn: filterDate.AddDays(5),
            createdOn: filterDate.AddDays(1),
            updatedOn: filterDate.AddDays(4),
            mergedOn: filterDate.AddDays(5),
            author: author,
            destination: new PullRequestDestination(new PullRequestBranch("develop")));
        var commentActivity = new PullRequestActivity(
            activityDate: filterDate.AddDays(2),
            mergeDate: null,
            actor: reviewerIdentity,
            comment: new ActivityComment(reviewerIdentity, filterDate.AddDays(2)),
            approval: null);
        var approvalActivity = new PullRequestActivity(
            activityDate: filterDate.AddDays(3),
            mergeDate: null,
            actor: reviewerIdentity,
            comment: null,
            approval: new ActivityApproval(reviewerIdentity, filterDate.AddDays(3)));

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        SetupPullRequestSize(client, new PullRequestSizeSummary(FilesChanged: 3, LinesAdded: 30, LinesRemoved: 20));
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 88),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([commentActivity, approvalActivity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 88),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(ToAsyncEnumerable([
                new PullRequestCommitInfo("commit-88-3", filterDate.AddDays(4), "Add PR size details"),
                new PullRequestCommitInfo("commit-88-2", filterDate.AddDays(2), "Fix reviewer aggregation"),
                new PullRequestCommitInfo("commit-88-1", filterDate.AddDays(1), "Initial PR setup")
            ]));
        client.Setup(x => x.GetCommitSizeAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                "commit-88-3",
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PullRequestSizeSummary(FilesChanged: 2, LinesAdded: 8, LinesRemoved: 2));
        client.Setup(x => x.GetCommitSizeAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                "commit-88-2",
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new PullRequestSizeSummary(FilesChanged: 1, LinesAdded: 3, LinesRemoved: 1));

        var analyzer = CreateAnalyzer(client.Object, new ActivityAnalyzer());

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        var authorKey = new DeveloperKey(new UserUuid("{alice-1}"));
        reportData.DeveloperStats.Should().ContainKey(authorKey);
        reportData.DeveloperStats[authorKey].AuthoredPullRequests.Should().ContainSingle();
        reportData.DeveloperStats[authorKey].AuthoredPullRequests[0].Id.Value.Should().Be(88);
        reportData.DeveloperStats[authorKey].CommitActivities.Should().HaveCount(2);
        reportData.DeveloperStats[authorKey].CommitActivities[0].CommitHash.Should().Be("commit-88-3");
        reportData.DeveloperStats[authorKey].CommitActivities[0].Message.Should().Be("Add PR size details");
        reportData.DeveloperStats[authorKey].CommitActivities[0].SizeSummary.LineChurn.Should().Be(10);
        reportData.DeveloperStats[authorKey].CommitActivities[1].SizeSummary.FilesChanged.Should().Be(1);

        var reviewerKey = new DeveloperKey(new UserUuid("{reviewer-1}"));
        reportData.DeveloperStats.Should().ContainKey(reviewerKey);
        reportData.DeveloperStats[reviewerKey].CommentActivities.Should().ContainSingle();
        reportData.DeveloperStats[reviewerKey].ApprovalActivities.Should().ContainSingle();
        reportData.DeveloperStats[reviewerKey].CommentActivities[0].PullRequestId.Value.Should().Be(88);
        reportData.DeveloperStats[reviewerKey].ApprovalActivities[0].PullRequestId.Value.Should().Be(88);
    }

    private static ReportParameters CreateParameters(
        DateTimeOffset filterDate,
        IReadOnlyList<BranchName>? branchNameList = null,
        string? teamFilter = null,
        bool showAllDetailsForDevelopers = false,
        DateTimeOffset? toDateExclusive = null)
    {
        return new ReportParameters(
            filterDate,
            new Workspace("ws"),
            new RepoNameFilter(string.Empty),
            repoNameList: [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            branchNameList ?? [],
            teamFilter: teamFilter,
            showAllDetailsForDevelopers: showAllDetailsForDevelopers,
            toDateExclusive: toDateExclusive);
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

    private void SetupPullRequestSize(
        Mock<IBitbucketClient> client,
        PullRequestSizeSummary? sizeSummary = null)
    {
        client.Setup(x => x.GetPullRequestSizeAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => !string.IsNullOrWhiteSpace(repoSlug.Value)),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value > 0),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(sizeSummary ?? PullRequestSizeSummary.Empty);
    }

    private static PullRequestAnalyzer CreateAnalyzer(
        IBitbucketClient client,
        IActivityAnalyzer activityAnalyzer,
        int pullRequestConcurrency = 1)
    {
        return new PullRequestAnalyzer(
            client,
            activityAnalyzer,
            CreateBitbucketOptions(pullRequestConcurrency));
    }

    private static IOptions<BitbucketOptions> CreateBitbucketOptions(int pullRequestConcurrency = 1)
    {
        return Options.Create(new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 50,
            PullRequestConcurrency = pullRequestConcurrency,
            RepositoryConcurrency = 1,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Pdf = new PdfOptions()
        });
    }
}


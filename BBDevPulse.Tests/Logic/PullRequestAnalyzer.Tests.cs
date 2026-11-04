using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Caching;
using BBDevPulse.Caching.Mappers;
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
    [Fact]
    public async Task RepositoryAndPullRequestLimitsComposeWithoutLosingResults()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repositories = new[]
        {
            new Repository(new RepoName("Repo A"), new RepoSlug("repo-a")),
            new Repository(new RepoName("Repo B"), new RepoSlug("repo-b"))
        };
        var author = new User(new DisplayName("Alice"), new UserUuid("{a}"));
        var prs = Enumerable.Range(1, 3).Select(id => new PullRequest(new PullRequestId(id),
            PullRequestState.Open, null, date, null, null, author, null)).ToArray();
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximum = 0;
        var sync = new object();
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable(prs));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => WaitForRelease());
        client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).ReturnsAsync(PullRequestSizeSummary.Empty);
        var analyzer = CreateAnalyzer(client.Object, new ActivityAnalyzer(), pullRequestConcurrency: 2);
        var accumulator = new ReportAccumulator(CreateParameters(date.AddDays(-1)));
        var coordinator = new RepositoryAnalysisCoordinator(2);
        var run = coordinator.RunAsync(repositories,
            (repo, token) => analyzer.AnalyzeAsync(repo, accumulator.WorkingData, token), _ => { }, CancellationToken.None);

        await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        maximum.Should().Be(4);
        release.SetResult();
        await run;
        var report = accumulator.Build();
        report.Reports.Should().HaveCount(6);
        report.DeveloperStats.Values.Single().PrsOpenedSince.Should().Be(6);
        maximum.Should().Be(4);
        active.Should().Be(0);

        async IAsyncEnumerable<PullRequestActivity> WaitForRelease()
        {
            var current = Interlocked.Increment(ref active);
            lock (sync)
            {
                maximum = System.Math.Max(maximum, current);
            }
            if (current == 4)
            {
                allStarted.TrySetResult();
            }
            try
            {
                await release.Task;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
            yield break;
        }
    }

    [Fact]
    public async Task PullRequestConcurrencyIsBoundedPerRepository()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var author = new User(new DisplayName("Alice"), new UserUuid("{a}"));
        var pullRequests = Enumerable.Range(1, 4)
            .Select(id => new PullRequest(new PullRequestId(id), PullRequestState.Open, null, date,
                null, null, author, null)).ToArray();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maxActive = 0;
        var sync = new object();
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable(pullRequests));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ReadActivities());
        client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).ReturnsAsync(PullRequestSizeSummary.Empty);
        var data = new ReportData(CreateParameters(date.AddDays(-1)));
        var run = CreateAnalyzer(client.Object, new ActivityAnalyzer(), pullRequestConcurrency: 2)
            .AnalyzeAsync(repository, data, CancellationToken.None);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        maxActive.Should().Be(2);
        release.SetResult();
        await run;
        maxActive.Should().Be(2);
        active.Should().Be(0);
        data.Reports.Should().HaveCount(4);

        async IAsyncEnumerable<PullRequestActivity> ReadActivities()
        {
            var current = Interlocked.Increment(ref active);
            lock (sync)
            {
                maxActive = System.Math.Max(maxActive, current);
            }
            if (current == 2)
            {
                started.TrySetResult();
            }
            try
            {
                await release.Task;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
            yield break;
        }
    }

    [Fact]
    public async Task AnalyzeAsyncObservesAuthorAndActivityIdentitiesBeforeTheyAreMerged()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var data = new ReportData(CreateParameters(date.AddDays(-1)));
        var author = new User(new DisplayName("Alice"), new UserUuid("{a}"));
        var reviewer = new DeveloperIdentity(new UserUuid("{b}"), new DisplayName("Alice"));
        var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null, author, null);
        var activity = new PullRequestActivity(date.AddHours(1), null, reviewer, new ActivityComment(reviewer, date.AddHours(1)), null);
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(), It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable([pr]));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(), It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable([activity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(), It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(), It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).ReturnsAsync(PullRequestSizeSummary.Empty);
        await CreateAnalyzer(client.Object, new ActivityAnalyzer()).AnalyzeAsync(repository, data, CancellationToken.None);
        data.GetIdentityCollisions().Should().ContainSingle().Which.Uuids.Should().Equal("{a}", "{b}");
        data.DeveloperStats.Should().HaveCount(2);
        data.DeveloperStats[new DeveloperKey(new UserUuid("{b}"))].PeerCommentsAfter.Should().Be(1);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TeamFilterIncludesBothSameNamedUsersButKeepsTheirStatisticsSeparate(bool reverse)
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var data = new ReportData(CreateParameters(date.AddDays(-1), teamFilter: "Core"),
            new Dictionary<DisplayName, PersonCsvRow> { [new DisplayName("Alice")] = new("Senior", "Core") });
        var first = new User(new DisplayName("Alice"), new UserUuid("{a}"));
        var second = new User(new DisplayName("Alice"), new UserUuid("{b}"));
        var firstPr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null, first, null);
        var secondPr = new PullRequest(new PullRequestId(2), PullRequestState.Open, null, date, null, null, second, null);
        var reviewer = second.ToDeveloperIdentity();
        var comment = new PullRequestActivity(date.AddHours(1), null, reviewer,
            new ActivityComment(reviewer, date.AddHours(1)), null);
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable<PullRequest>(reverse ? [secondPr, firstPr] : [firstPr, secondPr]));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.Is<PullRequestId>(id => id.Value == 1), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable([comment]));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.Is<PullRequestId>(id => id.Value == 2), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).ReturnsAsync(PullRequestSizeSummary.Empty);

        await CreateAnalyzer(client.Object, new ActivityAnalyzer()).AnalyzeAsync(repository, data, CancellationToken.None);

        data.Reports.Should().HaveCount(2).And.OnlyContain(report => report.IncludeInMetrics);
        data.DeveloperStats.Should().HaveCount(2);
        var firstStats = data.DeveloperStats[new DeveloperKey(new UserUuid("{a}"))];
        var secondStats = data.DeveloperStats[new DeveloperKey(new UserUuid("{b}"))];
        firstStats.PrsOpenedSince.Should().Be(1);
        firstStats.CommentsAfter.Should().Be(0);
        secondStats.PrsOpenedSince.Should().Be(1);
        secondStats.CommentsAfter.Should().Be(1);
        secondStats.PeerCommentsAfter.Should().Be(1);
        var collision = data.GetIdentityCollisions().Should().ContainSingle().Subject;
        collision.Name.Should().Be("Alice");
        collision.Uuids.Should().Equal("{a}", "{b}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TeamFilterUsesCsvUuidToSelectOnlyMatchingSameNamedUser(bool reverse)
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var data = new ReportData(CreateParameters(date.AddDays(-1), teamFilter: "Core"),
            new Dictionary<DisplayName, PersonCsvRow> { [new DisplayName("Alice")] = new("Senior", "Core", "a") });
        var first = new User(new DisplayName("Alice"), new UserUuid("{a}"));
        var second = new User(new DisplayName("Alice"), new UserUuid("{b}"));
        var firstPr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null, first, null);
        var secondPr = new PullRequest(new PullRequestId(2), PullRequestState.Open, null, date, null, null, second, null);
        var reviewer = second.ToDeveloperIdentity();
        var comment = new PullRequestActivity(date.AddHours(1), null, reviewer,
            new ActivityComment(reviewer, date.AddHours(1)), null);
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable<PullRequest>(reverse ? [secondPr, firstPr] : [firstPr, secondPr]));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.Is<PullRequestId>(id => id.Value == 1), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable([comment]));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.Is<PullRequestId>(id => id.Value == 2), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).ReturnsAsync(PullRequestSizeSummary.Empty);

        await CreateAnalyzer(client.Object, new ActivityAnalyzer()).AnalyzeAsync(repository, data, CancellationToken.None);

        data.Reports.Should().ContainSingle().Which.Id.Value.Should().Be(1);
        data.DeveloperStats.Should().ContainSingle();
        data.DeveloperStats[new DeveloperKey(new UserUuid("{a}"))].PrsOpenedSince.Should().Be(1);
        data.DeveloperStats.Should().NotContainKey(new DeveloperKey(new UserUuid("{b}")));
        data.GetIdentityCollisionNotices().Single().IsError.Should().BeFalse();
        var collision = data.GetIdentityCollisions().Should().ContainSingle().Subject;
        collision.Name.Should().Be("Alice");
        collision.Uuids.Should().Equal("{a}", "{b}");
    }

    [Fact]
    public async Task MissingAuthorUuidExcludesPullRequestAndRaisesWarning()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var data = new ReportData(CreateParameters(date.AddDays(-1)));
        var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null,
            new User(new DisplayName("Alice"), null), null);
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable([pr]));

        await CreateAnalyzer(client.Object, new ActivityAnalyzer()).AnalyzeAsync(repository, data, CancellationToken.None);

        data.HasMissingUserUuid.Should().BeTrue();
        data.Reports.Should().BeEmpty();
        data.DeveloperStats.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingReviewerUuidExcludesActivityAndRaisesWarning()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var data = new ReportData(CreateParameters(date.AddDays(-1), showAllDetailsForDevelopers: true));
        var author = new User(new DisplayName("Alice"), new UserUuid("{a}"));
        var reviewer = new DeveloperIdentity(null, new DisplayName("Reviewer"));
        var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null, author, null);
        var activity = new PullRequestActivity(date.AddHours(1), null, reviewer,
            new ActivityComment(reviewer, date.AddHours(1)), new ActivityApproval(reviewer, date.AddHours(1)));
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(), It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable([pr]));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(), It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable([activity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(), It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(), It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).ReturnsAsync(PullRequestSizeSummary.Empty);

        await CreateAnalyzer(client.Object, new ActivityAnalyzer()).AnalyzeAsync(repository, data, CancellationToken.None);

        data.HasMissingUserUuid.Should().BeTrue();
        data.DeveloperStats.Should().ContainSingle();
        var report = data.Reports.Should().ContainSingle().Subject;
        report.Comments.Should().Be(0);
        report.FirstReactionOn.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsyncStopsSchedulingAfterHandlerFailure()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var pullRequests = Enumerable.Range(1, 2).Select(id => new PullRequest(
            new PullRequestId(id), PullRequestState.Open, closedOn: null,
            createdOn: date, updatedOn: null, mergedOn: null, author: new User(new DisplayName("Author"), new UserUuid("{author}")), destination: null)).ToArray();
        var failure = new InvalidOperationException("activity failed");
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable(pullRequests));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Throws(failure);

        var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);
        var report = new ReportData(CreateParameters(date.AddDays(-1)));
        var thrown = await FluentActions.Invoking(() => analyzer.AnalyzeAsync(repository, report, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        thrown.Which.Should().BeSameAs(failure);
        client.Verify(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
            It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsyncDoesNotTreatPartialCommitPageAsComplete()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var data = new ReportData(CreateParameters(date.AddDays(-1)));
        var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open,
            closedOn: null, createdOn: date, updatedOn: null, mergedOn: null,
            author: new User(new DisplayName("Alice"), new UserUuid("{alice-1}")), destination: null);
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable([pr]));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()))
            .Returns(() => PartialCommits());
        client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestSizeSummary.Empty);

        static async IAsyncEnumerable<PullRequestCommitInfo> PartialCommits()
        {
            yield return new PullRequestCommitInfo("hash", new DateTimeOffset(2026, 2, 24, 9, 0, 0, TimeSpan.Zero), "fix");
            yield return new PullRequestCommitInfo("initial", new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero), "initial change");
            await Task.Yield();
            throw new InvalidOperationException("second page failed");
        }

        var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);
        await analyzer.AnalyzeAsync(repository, data, CancellationToken.None);

        data.Reports.Single().HasCorrectionsData.Should().BeFalse();
        data.Reports.Single().HasSizeData.Should().BeTrue();
        data.DeveloperStats.Values.Single().HasCompleteCorrectionsData.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsyncPropagatesCancellationWhenSupplementalRequestFails()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open,
            closedOn: null, createdOn: date, updatedOn: null, mergedOn: null,
            author: new User(new DisplayName("Author"), new UserUuid("{author}")), destination: null);
        using var cancellation = new CancellationTokenSource();
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable([pr]));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromException<PullRequestSizeSummary>(new InvalidOperationException("request failed during cancellation"));
            });

        var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);
        var report = new ReportData(CreateParameters(date.AddDays(-1)));

        await FluentActions.Invoking(() => analyzer.AnalyzeAsync(repository, report, cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        report.Reports.Should().BeEmpty();
    }

    [Fact]
    public async Task ColdAndWarmCachePreserveMetricsWarningsAndUnavailableMeasurements()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "BBDevPulse.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
            var parameters = CreateParameters(date.AddDays(-1), showAllDetailsForDevelopers: true);
            var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
            var first = new User(new DisplayName("Alice"), new UserUuid("{a}"));
            var second = new User(new DisplayName("Alice"), new UserUuid("{b}"));
            var prs = new[]
            {
                new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null, first, null),
                new PullRequest(new PullRequestId(2), PullRequestState.Open, null, date, null, null, second, null)
            };
            var reviewer = second.ToDeveloperIdentity();
            var missing = new DeveloperIdentity(null, new DisplayName("Unknown reviewer"));
            var activities = new[]
            {
                new PullRequestActivity(date.AddHours(1), null, reviewer,
                    new ActivityComment(reviewer, date.AddHours(1)), null),
                new PullRequestActivity(date.AddHours(2), null, missing,
                    new ActivityComment(missing, date.AddHours(2)), null)
            };
            var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
            client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable(prs));
            client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(id => id.Value == 1), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
                .Returns(() => ToAsyncEnumerable(activities));
            client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(id => id.Value == 2), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
                .Returns(() => ToAsyncEnumerable<PullRequestActivity>([]));
            client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
            client.Setup(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("unavailable"));
            var cache = new FilePullRequestAnalysisCache(new PullRequestAnalysisCacheMapper(),
                new Mock<IBitbucketTelemetryService>().Object, cacheDirectory);
            var analyzer = CreateAnalyzer(client.Object, new ActivityAnalyzer(), analysisCache: cache);
            var cold = new ReportData(parameters);
            var warm = new ReportData(parameters);
            await analyzer.AnalyzeAsync(repository, cold, CancellationToken.None);
            await analyzer.AnalyzeAsync(repository, warm, CancellationToken.None);

            cold.HasMissingUserUuid.Should().BeTrue();
            warm.HasMissingUserUuid.Should().BeTrue();
            cold.GetIdentityCollisions().Single().Uuids.Should().Equal("{a}", "{b}");
            warm.GetIdentityCollisions().Single().Uuids.Should().Equal("{a}", "{b}");
            cold.Reports.Select(report => (report.Id.Value, report.Comments, report.FirstReactionOn, report.SizeAvailability))
                .Should().BeEquivalentTo(warm.Reports.Select(report => (report.Id.Value, report.Comments, report.FirstReactionOn, report.SizeAvailability)));
            cold.Reports.Should().OnlyContain(report => report.SizeAvailability == DataAvailability.Unavailable);
            warm.DeveloperStats[new DeveloperKey(new UserUuid("{b}"))].PeerCommentsAfter.Should().Be(1);
            cold.DeveloperStats[new DeveloperKey(new UserUuid("{b}"))].PeerCommentsAfter.Should().Be(1);
            client.Verify(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
                Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsyncRetriesMissingSizeThenUsesUpdatedDiskSnapshot()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "BBDevPulse.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
            var parameters = CreateParameters(date.AddDays(-1), showAllDetailsForDevelopers: true);
            var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
            var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open,
                closedOn: null, createdOn: date, updatedOn: null, mergedOn: null,
                author: new User(new DisplayName("Alice"), new UserUuid("{alice-1}")), destination: null);
            var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
            client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                    It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>()))
                .Returns(() => ToAsyncEnumerable([pr]));
            client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                    It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
                .Returns(() => ToAsyncEnumerable<PullRequestActivity>([]));
            client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                    It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()))
                .Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>(
                [
                    new PullRequestCommitInfo("first", date.AddHours(2), "first correction"),
                    new PullRequestCommitInfo("second", date.AddHours(1), "second correction")
                ]));
            client.SetupSequence(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                    It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("diffstat unavailable"))
                .ReturnsAsync(new PullRequestSizeSummary(2, 4, 1));
            client.Setup(x => x.GetCommitSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                    "first", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PullRequestSizeSummary(1, 3, 1));
            client.SetupSequence(x => x.GetCommitSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                    "second", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("commit diffstat unavailable"))
                .ReturnsAsync(new PullRequestSizeSummary(1, 2, 0));
            var cache = new FilePullRequestAnalysisCache(new PullRequestAnalysisCacheMapper(),
                new Mock<IBitbucketTelemetryService>().Object, cacheDirectory);
            var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object,
                analysisCache: cache);

            var first = new ReportData(parameters);
            await analyzer.AnalyzeAsync(repository, first, CancellationToken.None);
            first.Reports.Single().HasSizeData.Should().BeFalse();
            first.Reports.Single().HasCorrectionsData.Should().BeTrue();

            var second = new ReportData(parameters);
            await analyzer.AnalyzeAsync(repository, second, CancellationToken.None);
            second.Reports.Single().FilesChanged.Should().Be(2);
            second.Reports.Single().HasSizeData.Should().BeTrue();

            var third = new ReportData(parameters);
            await analyzer.AnalyzeAsync(repository, third, CancellationToken.None);
            third.Reports.Single().FilesChanged.Should().Be(2);
            second.DeveloperStats.Values.Single().CommitActivities.Should()
                .OnlyContain(activity => activity.HasSizeData);
            client.Verify(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            client.Verify(x => x.GetCommitSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                "first", It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.GetCommitSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                "second", It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
        finally
        {
            var resolved = Path.GetFullPath(cacheDirectory);
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BBDevPulse.Tests"));
            if (resolved.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(resolved))
            {
                Directory.Delete(resolved, recursive: true);
            }
        }
    }

    [Fact]
    public async Task IncompleteDiffstatIsRetriedWithoutLosingKnownFileCount()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "BBDevPulse.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
            var parameters = CreateParameters(date.AddDays(-1));
            var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
            var pr = new PullRequest(new PullRequestId(1), PullRequestState.Open, null, date, null, null,
                new User(new DisplayName("Alice"), new UserUuid("{a}")), null);
            var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
            client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable([pr]));
            client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
                .Returns(() => ToAsyncEnumerable<PullRequestActivity>([]));
            client.Setup(x => x.GetPullRequestCommitsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>())).Returns(() => ToAsyncEnumerable<PullRequestCommitInfo>([]));
            client.SetupSequence(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                    It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PullRequestSizeSummary(2, 3, 0, hasLineData: false))
                .ThrowsAsync(new InvalidOperationException("temporary error"))
                .ReturnsAsync(new PullRequestSizeSummary(2, 3, 1));
            var cache = new FilePullRequestAnalysisCache(new PullRequestAnalysisCacheMapper(),
                new Mock<IBitbucketTelemetryService>().Object, cacheDirectory);
            var analyzer = CreateAnalyzer(client.Object, new ActivityAnalyzer(), analysisCache: cache);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                var report = new ReportData(parameters);
                await analyzer.AnalyzeAsync(repository, report, CancellationToken.None);
                var entry = report.Reports.Single();
                entry.HasSizeDataForMode(PullRequestSizeMode.Files).Should().BeTrue();
                entry.GetSizeMetricValue(PullRequestSizeMode.Files).Should().Be(2);
                entry.HasSizeDataForMode(PullRequestSizeMode.Lines).Should().Be(attempt == 2);
            }
            client.Verify(x => x.GetPullRequestSizeAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
                Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsyncWaitsForActiveHandlerAndPreservesPageFailure()
    {
        var date = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        var repository = new Repository(new RepoName("Repo"), new RepoSlug("repo"));
        var data = new ReportData(CreateParameters(date.AddDays(-1)));
        var pullRequest = new PullRequest(new PullRequestId(1), PullRequestState.Open,
            closedOn: null, createdOn: date, updatedOn: null, mergedOn: null,
            author: new User(new DisplayName("Author"), new UserUuid("{author}")), destination: null);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceFailure = new InvalidOperationException("second page failed");
        var active = 0;
        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(), It.IsAny<CancellationToken>()))
            .Returns((Workspace _, RepoSlug _, Func<PullRequest, bool> _, CancellationToken token) => ReadPullRequests(token));
        client.Setup(x => x.GetPullRequestActivityAsync(It.IsAny<Workspace>(), It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(), It.IsAny<Func<PullRequestActivity, bool>>(), It.IsAny<CancellationToken>()))
            .Returns((Workspace _, RepoSlug _, PullRequestId _, Func<PullRequestActivity, bool> _, CancellationToken token) => ReadActivities(token));

        async IAsyncEnumerable<PullRequest> ReadPullRequests([EnumeratorCancellation] CancellationToken token)
        {
            yield return pullRequest;
            await started.Task;
            token.ThrowIfCancellationRequested();
            throw sourceFailure;
        }

        async IAsyncEnumerable<PullRequestActivity> ReadActivities([EnumeratorCancellation] CancellationToken token)
        {
            Interlocked.Increment(ref active);
            started.SetResult();
            try
            {
                await finish.Task;
                token.ThrowIfCancellationRequested();
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }

            yield break;
        }

        var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);
        var run = analyzer.AnalyzeAsync(repository, data, CancellationToken.None);
        await started.Task;
        run.IsCompleted.Should().BeFalse();
        finish.SetResult();
        var thrown = await FluentActions.Invoking(async () => await run).Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(sourceFailure);
        active.Should().Be(0);
    }

    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "Constructor throws when client is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenClientIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketClient client = null!;
        var options = CreateBitbucketOptions();

        // Act
        Action act = () => _ = new PullRequestAnalyzer(
            client,
            new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object,
            new Mock<IPullRequestAnalysisCache>(MockBehavior.Strict).Object,
            new PullRequestAnalysisCacheKeyFactory(),
            options);

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
        Action act = () => _ = new PullRequestAnalyzer(
            new Mock<IBitbucketClient>(MockBehavior.Strict).Object,
            activityAnalyzer,
            new Mock<IPullRequestAnalysisCache>(MockBehavior.Strict).Object,
            new PullRequestAnalysisCacheKeyFactory(),
            options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when analysis cache is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenAnalysisCacheIsNullThrowsArgumentNullException()
    {
        // Arrange
        IPullRequestAnalysisCache analysisCache = null!;
        var options = CreateBitbucketOptions();

        // Act
        Action act = () => _ = new PullRequestAnalyzer(
            new Mock<IBitbucketClient>(MockBehavior.Strict).Object,
            new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object,
            analysisCache,
            new PullRequestAnalysisCacheKeyFactory(),
            options);

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
        Action act = () => _ = new PullRequestAnalyzer(
            client,
            activityAnalyzer,
            new Mock<IPullRequestAnalysisCache>(MockBehavior.Strict).Object,
            new PullRequestAnalysisCacheKeyFactory(),
            options);

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
            author: new User(new DisplayName("Author"), new UserUuid("{author}")),
            destination: new PullRequestDestination(new PullRequestBranch("develop")));

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        SetupPullRequestSize(client);
        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        var activityFetchCalls = 0;
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 1),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 1),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
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
                    author: new User(new DisplayName("Author"), new UserUuid("{author}")),
                    destination: null);
                _ = shouldStop(historicalPullRequest);
                return ToAsyncEnumerable([pullRequest]);
            });
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 42),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
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
                var authorIdentity = author.ToDeveloperIdentity();
                var authorKey = authorIdentity.ToKey();
                analysis.Participants[authorKey] = authorIdentity;
                analysis.Participants[reviewerKey] = reviewerIdentity;
                analysis.CommentCounts[authorKey] = 2;
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
        reportData.DeveloperStats[authorKey].CommentsAfter.Should().Be(2);
        reportData.DeveloperStats[authorKey].PeerCommentsAfter.Should().Be(0);
        reportData.DeveloperStats[authorKey].Corrections.Should().Be(2);

        var reviewerKeyForStats = new DeveloperKey(new UserUuid("{reviewer-1}"));
        reportData.DeveloperStats.Should().ContainKey(reviewerKeyForStats);
        reportData.DeveloperStats[reviewerKeyForStats].CommentsAfter.Should().Be(3);
        reportData.DeveloperStats[reviewerKeyForStats].PeerCommentsAfter.Should().Be(3);
        reportData.DeveloperStats[reviewerKeyForStats].ApprovalsAfter.Should().Be(2);
        pullRequestStopPredicateCalled.Should().BeTrue();
        activityStopPredicateCalled.Should().BeTrue();
    }

    [Fact(DisplayName = "AnalyzeAsync excludes PR and warns when author is missing")]
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
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 3),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 3),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable<PullRequestCommitInfo>([]));

        var analyzer = CreateAnalyzer(client.Object, new Mock<IActivityAnalyzer>(MockBehavior.Strict).Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().BeEmpty();
        reportData.HasMissingUserUuid.Should().BeTrue();
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
            author: new User(new DisplayName("Author"), new UserUuid("{author}")),
            destination: null);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        SetupPullRequestSize(client);
        client.Setup(x => x.GetPullRequestsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<Func<PullRequest, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 4),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 4),
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 5),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([activity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 5),
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 6),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable<PullRequestActivity>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 6),
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([outsiderPullRequest, teamPullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 10),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([outsiderCommentActivity, outsiderApprovalActivity]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 11),
                It.Is<Func<PullRequestActivity, bool>>(shouldStop => shouldStop != null),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([teamCommentActivity, teamMergeActivity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 10),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable<PullRequestCommitInfo>([]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 11),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([
                new PullRequestCommitInfo("commit-11-3", filterDate.AddDays(5), "Newest team fix"),
                new PullRequestCommitInfo("commit-11-2", filterDate.AddDays(3), "In-range fix"),
                new PullRequestCommitInfo("commit-11-1", filterDate.AddDays(2), "Oldest in-range fix")
            ]));
        client.Setup(x => x.GetPullRequestSizeAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestSizeSummary.Empty);
        client.Setup(x => x.GetPullRequestSizeAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 11),
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 77),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([activity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 77),
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));
        client.Setup(x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 88),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([commentActivity, approvalActivity]));
        client.Setup(x => x.GetPullRequestCommitsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 88),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([
                new PullRequestCommitInfo("commit-88-3", filterDate.AddDays(4), "Add PR size details"),
                new PullRequestCommitInfo("commit-88-2", filterDate.AddDays(2), "Fix reviewer aggregation"),
                new PullRequestCommitInfo("commit-88-1", filterDate.AddDays(1), "Initial PR setup")
            ]));
        client.Setup(x => x.GetCommitSizeAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                "commit-88-3",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestSizeSummary(FilesChanged: 2, LinesAdded: 8, LinesRemoved: 2));
        client.Setup(x => x.GetCommitSizeAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                "commit-88-2",
                It.IsAny<CancellationToken>()))
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

    [Fact(DisplayName = "AnalyzeAsync reuses cached snapshot and skips Bitbucket enrichment requests")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsyncWhenSnapshotExistsUsesCache()
    {
        // Arrange
        var filterDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var parameters = CreateParameters(filterDate, branchNameList: [new BranchName("develop")], showAllDetailsForDevelopers: true);
        var reportData = new ReportData(parameters);
        var repository = new Repository(new RepoName("Repo Name"), new RepoSlug("repo-slug"));
        var author = new User(new DisplayName("Alice"), new UserUuid("{alice-1}"));
        var reviewerIdentity = new DeveloperIdentity(new UserUuid("{reviewer-1}"), new DisplayName("Reviewer"));
        var pullRequest = new PullRequest(
            new PullRequestId(42),
            PullRequestState.Declined,
            closedOn: filterDate.AddDays(3),
            createdOn: filterDate.AddDays(1),
            updatedOn: filterDate.AddDays(2),
            mergedOn: null,
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
        var snapshot = new PullRequestAnalysisSnapshot(
            activities: [commentActivity, approvalActivity],
            correctionCommits:
            [
                new PullRequestCommitInfo("commit-42-3", filterDate.AddDays(4), "Newest cached fix"),
                new PullRequestCommitInfo("commit-42-2", filterDate.AddDays(2), "Cached in-range fix")
            ],
            sizeSummary: new PullRequestSizeSummary(FilesChanged: 5, LinesAdded: 40, LinesRemoved: 10),
            commitActivities:
            [
                new DeveloperCommitActivity(
                    "Repo Name",
                    "repo-slug",
                    new PullRequestId(42),
                    "commit-42-3",
                    "Newest cached fix",
                    filterDate.AddDays(4),
                    new PullRequestSizeSummary(FilesChanged: 2, LinesAdded: 7, LinesRemoved: 1))
            ],
            hasEnrichment: true);

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetPullRequestsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<Func<PullRequest, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([pullRequest]));

        var activityAnalyzer = new Mock<IActivityAnalyzer>(MockBehavior.Strict);
        activityAnalyzer.Setup(x => x.Analyze(
                It.IsAny<ActivityAnalysisState>(),
                commentActivity,
                It.IsAny<ReportParameters>()))
            .Callback<ActivityAnalysisState, PullRequestActivity, ReportParameters>((analysis, _, _) =>
            {
                analysis.HasActivityInRange = true;
                analysis.LastActivity = filterDate.AddDays(2);
                analysis.TotalComments = 1;
                analysis.FirstReactionOn = filterDate.AddDays(2);
                analysis.Participants[reviewerIdentity.ToKey()] = reviewerIdentity;
                analysis.CommentCounts[reviewerIdentity.ToKey()] = 1;
            });
        activityAnalyzer.Setup(x => x.Analyze(
                It.IsAny<ActivityAnalysisState>(),
                approvalActivity,
                It.IsAny<ReportParameters>()))
            .Callback<ActivityAnalysisState, PullRequestActivity, ReportParameters>((analysis, _, _) =>
            {
                analysis.HasActivityInRange = true;
                analysis.LastActivity = filterDate.AddDays(3);
                analysis.Participants[reviewerIdentity.ToKey()] = reviewerIdentity;
                analysis.ApprovalCounts[reviewerIdentity.ToKey()] = 1;
            });

        var analysisCache = new Mock<IPullRequestAnalysisCache>(MockBehavior.Strict);
        analysisCache.Setup(x => x.TryGet(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => repoSlug.Value == "repo-slug"),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value == 42),
                It.IsAny<string>(),
                It.IsAny<string>(),
                out snapshot))
            .Returns(true);

        var analyzer = CreateAnalyzer(client.Object, activityAnalyzer.Object, analysisCache: analysisCache.Object);

        // Act
        await analyzer.AnalyzeAsync(repository, reportData, cancellationToken);

        // Assert
        reportData.Reports.Should().ContainSingle();
        reportData.Reports[0].Corrections.Should().Be(2);
        reportData.Reports[0].FilesChanged.Should().Be(5);
        reportData.Reports[0].LinesAdded.Should().Be(40);
        reportData.Reports[0].LinesRemoved.Should().Be(10);

        var authorKey = new DeveloperKey(new UserUuid("{alice-1}"));
        reportData.DeveloperStats.Should().ContainKey(authorKey);
        reportData.DeveloperStats[authorKey].CommitActivities.Should().ContainSingle();
        reportData.DeveloperStats[authorKey].CommitActivities[0].CommitHash.Should().Be("commit-42-3");

        client.Verify(
            x => x.GetPullRequestActivityAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<Func<PullRequestActivity, bool>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        client.Verify(
            x => x.GetPullRequestCommitsAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        client.Verify(
            x => x.GetPullRequestSizeAsync(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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

    private static void SetupPullRequestSize(
        Mock<IBitbucketClient> client,
        PullRequestSizeSummary? sizeSummary = null)
    {
        client.Setup(x => x.GetPullRequestSizeAsync(
                It.Is<Workspace>(workspace => workspace.Value == "ws"),
                It.Is<RepoSlug>(repoSlug => !string.IsNullOrWhiteSpace(repoSlug.Value)),
                It.Is<PullRequestId>(pullRequestId => pullRequestId.Value > 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sizeSummary ?? PullRequestSizeSummary.Empty);
    }

    private static PullRequestAnalyzer CreateAnalyzer(
        IBitbucketClient client,
        IActivityAnalyzer activityAnalyzer,
        int pullRequestConcurrency = 1,
        IPullRequestAnalysisCache? analysisCache = null)
    {
        var resolvedAnalysisCache = analysisCache ?? CreateCacheMissAnalysisCache();

        return new PullRequestAnalyzer(
            client,
            activityAnalyzer,
            resolvedAnalysisCache,
            new PullRequestAnalysisCacheKeyFactory(),
            CreateBitbucketOptions(pullRequestConcurrency));
    }

    private static IPullRequestAnalysisCache CreateCacheMissAnalysisCache()
    {
        var cache = new Mock<IPullRequestAnalysisCache>(MockBehavior.Strict);
        PullRequestAnalysisSnapshot snapshot = null!;

        cache.Setup(x => x.TryGet(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                out snapshot))
            .Returns(false);
        cache.Setup(x => x.Store(
                It.IsAny<Workspace>(),
                It.IsAny<RepoSlug>(),
                It.IsAny<PullRequestId>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PullRequestAnalysisSnapshot>()));

        return cache.Object;
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


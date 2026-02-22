using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;
using BBDevPulse.Presentation;

using Moq;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreReportPresenterTests
{
    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "Constructor throws when auth presenter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenAuthPresenterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IAuthPresenter authPresenter = null!;

        // Act
        Action act = () => _ = new SpectreReportPresenter(
            authPresenter,
            new Mock<IRepositoryListPresenter>(MockBehavior.Strict).Object,
            new Mock<IRepositoryAnalysisPresenter>(MockBehavior.Strict).Object,
            new Mock<IBranchFilterPresenter>(MockBehavior.Strict).Object,
            new Mock<IPullRequestReportPresenter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsPresenter>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when repository list presenter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRepositoryListPresenterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IRepositoryListPresenter repositoryListPresenter = null!;

        // Act
        Action act = () => _ = new SpectreReportPresenter(
            new Mock<IAuthPresenter>(MockBehavior.Strict).Object,
            repositoryListPresenter,
            new Mock<IRepositoryAnalysisPresenter>(MockBehavior.Strict).Object,
            new Mock<IBranchFilterPresenter>(MockBehavior.Strict).Object,
            new Mock<IPullRequestReportPresenter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsPresenter>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when repository analysis presenter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRepositoryAnalysisPresenterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IRepositoryAnalysisPresenter repositoryAnalysisPresenter = null!;

        // Act
        Action act = () => _ = new SpectreReportPresenter(
            new Mock<IAuthPresenter>(MockBehavior.Strict).Object,
            new Mock<IRepositoryListPresenter>(MockBehavior.Strict).Object,
            repositoryAnalysisPresenter,
            new Mock<IBranchFilterPresenter>(MockBehavior.Strict).Object,
            new Mock<IPullRequestReportPresenter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsPresenter>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when branch filter presenter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenBranchFilterPresenterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBranchFilterPresenter branchFilterPresenter = null!;

        // Act
        Action act = () => _ = new SpectreReportPresenter(
            new Mock<IAuthPresenter>(MockBehavior.Strict).Object,
            new Mock<IRepositoryListPresenter>(MockBehavior.Strict).Object,
            new Mock<IRepositoryAnalysisPresenter>(MockBehavior.Strict).Object,
            branchFilterPresenter,
            new Mock<IPullRequestReportPresenter>(MockBehavior.Strict).Object,
            new Mock<IStatisticsPresenter>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when pull request report presenter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenPullRequestReportPresenterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IPullRequestReportPresenter pullRequestReportPresenter = null!;

        // Act
        Action act = () => _ = new SpectreReportPresenter(
            new Mock<IAuthPresenter>(MockBehavior.Strict).Object,
            new Mock<IRepositoryListPresenter>(MockBehavior.Strict).Object,
            new Mock<IRepositoryAnalysisPresenter>(MockBehavior.Strict).Object,
            new Mock<IBranchFilterPresenter>(MockBehavior.Strict).Object,
            pullRequestReportPresenter,
            new Mock<IStatisticsPresenter>(MockBehavior.Strict).Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when statistics presenter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenStatisticsPresenterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IStatisticsPresenter statisticsPresenter = null!;

        // Act
        Action act = () => _ = new SpectreReportPresenter(
            new Mock<IAuthPresenter>(MockBehavior.Strict).Object,
            new Mock<IRepositoryListPresenter>(MockBehavior.Strict).Object,
            new Mock<IRepositoryAnalysisPresenter>(MockBehavior.Strict).Object,
            new Mock<IBranchFilterPresenter>(MockBehavior.Strict).Object,
            new Mock<IPullRequestReportPresenter>(MockBehavior.Strict).Object,
            statisticsPresenter);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnnounceAuthAsync delegates to auth presenter")]
    [Trait("Category", "Unit")]
    public async Task AnnounceAuthAsyncWhenCalledDelegatesToAuthPresenter()
    {
        // Arrange
        var authPresenter = new Mock<IAuthPresenter>(MockBehavior.Strict);
        var announceCalls = 0;
        authPresenter.Setup(x => x.AnnounceAuthAsync(
                It.IsAny<Func<CancellationToken, Task<AuthUser>>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => announceCalls++)
            .Returns(Task.CompletedTask);
        var sut = CreateSut(authPresenter: authPresenter.Object);

        // Act
        await sut.AnnounceAuthAsync(
            _ => Task.FromResult(new AuthUser(new DisplayName("Alice"), new Username("alice"), new UserUuid("{uuid}"))),
            cancellationToken);

        // Assert
        announceCalls.Should().Be(1);
    }

    [Fact(DisplayName = "FetchRepositoriesAsync delegates to repository list presenter")]
    [Trait("Category", "Unit")]
    public async Task FetchRepositoriesAsyncWhenCalledDelegatesToRepositoryListPresenter()
    {
        // Arrange
        var expected = (IReadOnlyList<Repository>)new List<Repository> { CreateRepository("RepoA", "repoa") };
        var repositoryListPresenter = new Mock<IRepositoryListPresenter>(MockBehavior.Strict);
        var fetchCalls = 0;
        repositoryListPresenter.Setup(x => x.FetchRepositoriesAsync(
                It.IsAny<Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => fetchCalls++)
            .ReturnsAsync(expected);
        var sut = CreateSut(repositoryListPresenter: repositoryListPresenter.Object);

        // Act
        var result = await sut.FetchRepositoriesAsync((_, _) => AsyncEmpty(), cancellationToken);

        // Assert
        result.Should().BeSameAs(expected);
        fetchCalls.Should().Be(1);
    }

    [Fact(DisplayName = "AnalyzeRepositoriesAsync delegates to repository analysis presenter")]
    [Trait("Category", "Unit")]
    public async Task AnalyzeRepositoriesAsyncWhenCalledDelegatesToRepositoryAnalysisPresenter()
    {
        // Arrange
        var repositories = (IReadOnlyList<Repository>)new List<Repository> { CreateRepository("RepoA", "repoa") };
        var repositoryAnalysisPresenter = new Mock<IRepositoryAnalysisPresenter>(MockBehavior.Strict);
        var analyzeCalls = 0;
        repositoryAnalysisPresenter.Setup(x => x.AnalyzeRepositoriesAsync(
                repositories,
                It.IsAny<Func<Repository, CancellationToken, Task>>(),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => analyzeCalls++)
            .Returns(Task.CompletedTask);
        var sut = CreateSut(repositoryAnalysisPresenter: repositoryAnalysisPresenter.Object);

        // Act
        await sut.AnalyzeRepositoriesAsync(repositories, (_, _) => Task.CompletedTask, cancellationToken);

        // Assert
        analyzeCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderRepositoryTable throws when parameters are null")]
    [Trait("Category", "Unit")]
    public void RenderRepositoryTableWhenParametersAreNullThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        ReportParameters parameters = null!;

        // Act
        Action act = () => sut.RenderRepositoryTable([], parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderRepositoryTable delegates using parameters breakdown")]
    [Trait("Category", "Unit")]
    public void RenderRepositoryTableWhenCalledDelegatesToRepositoryListPresenter()
    {
        // Arrange
        var repositories = (IReadOnlyCollection<Repository>)new List<Repository> { CreateRepository("RepoA", "repoa") };
        var parameters = CreateReportParameters();
        var repositoryListPresenter = new Mock<IRepositoryListPresenter>(MockBehavior.Strict);
        var renderCalls = 0;
        repositoryListPresenter.Setup(x => x.RenderRepositoryTable(
                repositories,
                parameters.RepoSearchMode,
                parameters.RepoNameFilter,
                parameters.RepoNameList))
            .Callback(() => renderCalls++);
        var sut = CreateSut(repositoryListPresenter: repositoryListPresenter.Object);

        // Act
        sut.RenderRepositoryTable(repositories, parameters);

        // Assert
        renderCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderBranchFilterInfo throws when parameters are null")]
    [Trait("Category", "Unit")]
    public void RenderBranchFilterInfoWhenParametersAreNullThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        ReportParameters parameters = null!;

        // Act
        Action act = () => sut.RenderBranchFilterInfo(parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderBranchFilterInfo delegates branch list to branch filter presenter")]
    [Trait("Category", "Unit")]
    public void RenderBranchFilterInfoWhenCalledDelegatesToBranchFilterPresenter()
    {
        // Arrange
        var parameters = CreateReportParameters();
        var branchFilterPresenter = new Mock<IBranchFilterPresenter>(MockBehavior.Strict);
        var renderCalls = 0;
        branchFilterPresenter.Setup(x => x.RenderBranchFilterInfo(parameters.BranchNameList))
            .Callback(() => renderCalls++);
        var sut = CreateSut(branchFilterPresenter: branchFilterPresenter.Object);

        // Act
        sut.RenderBranchFilterInfo(parameters);

        // Assert
        renderCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RenderReport throws when report data is null")]
    [Trait("Category", "Unit")]
    public void RenderReportWhenReportDataIsNullThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        ReportData reportData = null!;

        // Act
        Action act = () => sut.RenderReport(reportData);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderReport sorts reports and delegates to pull request and statistics presenters")]
    [Trait("Category", "Unit")]
    public void RenderReportWhenCalledSortsAndDelegates()
    {
        // Arrange
        var parameters = CreateReportParameters();
        var reportData = new ReportData(parameters);
        reportData.Reports.Add(CreateReport(2, new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero)));
        reportData.Reports.Add(CreateReport(1, new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)));

        var pullRequestReportPresenter = new Mock<IPullRequestReportPresenter>(MockBehavior.Strict);
        var renderPullRequestCalls = 0;
        pullRequestReportPresenter.Setup(x => x.RenderPullRequestTable(reportData, parameters.FilterDate))
            .Callback(() => renderPullRequestCalls++);

        var statisticsPresenter = new Mock<IStatisticsPresenter>(MockBehavior.Strict);
        var renderMergeTimeStatsCalls = 0;
        var renderTtfrStatsCalls = 0;
        var renderCorrectionsStatsCalls = 0;
        var renderWorstPullRequestsCalls = 0;
        var renderDeveloperStatsCalls = 0;
        statisticsPresenter.Setup(x => x.RenderMergeTimeStats(reportData))
            .Callback(() => renderMergeTimeStatsCalls++);
        statisticsPresenter.Setup(x => x.RenderTtfrStats(reportData))
            .Callback(() => renderTtfrStatsCalls++);
        statisticsPresenter.Setup(x => x.RenderCorrectionsStats(reportData))
            .Callback(() => renderCorrectionsStatsCalls++);
        statisticsPresenter.Setup(x => x.RenderWorstPullRequestsTable(reportData))
            .Callback(() => renderWorstPullRequestsCalls++);
        statisticsPresenter.Setup(x => x.RenderDeveloperStatsTable(reportData, parameters.FilterDate))
            .Callback(() => renderDeveloperStatsCalls++);

        var sut = CreateSut(
            pullRequestReportPresenter: pullRequestReportPresenter.Object,
            statisticsPresenter: statisticsPresenter.Object);

        // Act
        sut.RenderReport(reportData);

        // Assert
        reportData.Reports.Select(report => report.Id.Value).Should().Equal(1, 2);
        renderPullRequestCalls.Should().Be(1);
        renderMergeTimeStatsCalls.Should().Be(1);
        renderTtfrStatsCalls.Should().Be(1);
        renderCorrectionsStatsCalls.Should().Be(1);
        renderWorstPullRequestsCalls.Should().Be(1);
        renderDeveloperStatsCalls.Should().Be(1);
    }

    private static SpectreReportPresenter CreateSut(
        IAuthPresenter? authPresenter = null,
        IRepositoryListPresenter? repositoryListPresenter = null,
        IRepositoryAnalysisPresenter? repositoryAnalysisPresenter = null,
        IBranchFilterPresenter? branchFilterPresenter = null,
        IPullRequestReportPresenter? pullRequestReportPresenter = null,
        IStatisticsPresenter? statisticsPresenter = null)
    {
        return new SpectreReportPresenter(
            authPresenter ?? new Mock<IAuthPresenter>(MockBehavior.Strict).Object,
            repositoryListPresenter ?? new Mock<IRepositoryListPresenter>(MockBehavior.Strict).Object,
            repositoryAnalysisPresenter ?? new Mock<IRepositoryAnalysisPresenter>(MockBehavior.Strict).Object,
            branchFilterPresenter ?? new Mock<IBranchFilterPresenter>(MockBehavior.Strict).Object,
            pullRequestReportPresenter ?? new Mock<IPullRequestReportPresenter>(MockBehavior.Strict).Object,
            statisticsPresenter ?? new Mock<IStatisticsPresenter>(MockBehavior.Strict).Object);
    }

    private static ReportParameters CreateReportParameters()
    {
        return new ReportParameters(
            filterDate: new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
            workspace: new Workspace("workspace"),
            repoNameFilter: new RepoNameFilter("filter"),
            repoNameList: [new RepoName("RepoA")],
            repoSearchMode: RepoSearchMode.FilterFromTheList,
            prTimeFilterMode: PrTimeFilterMode.CreatedOnOnly,
            branchNameList: [new BranchName("develop")]);
    }

    private static PullRequestReport CreateReport(int id, DateTimeOffset createdOn)
    {
        return new PullRequestReport(
            repository: "RepoA",
            repositorySlug: "repoa",
            author: "Alice",
            targetBranch: "develop",
            createdOn: createdOn,
            lastActivity: createdOn,
            mergedOn: null,
            rejectedOn: null,
            state: PullRequestState.Open,
            id: new PullRequestId(id),
            comments: 0,
            firstReactionOn: null);
    }

    private static async IAsyncEnumerable<Repository> AsyncEmpty()
    {
        await Task.Yield();
        yield break;
    }

    private static Repository CreateRepository(string name, string slug)
    {
        return new Repository(new RepoName(name), new RepoSlug(slug));
    }
}

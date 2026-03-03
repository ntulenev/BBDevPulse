using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Logic;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

using Moq;

namespace BBDevPulse.Tests.Logic;

public sealed class ReportRunnerTests
{
    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "Constructor throws when client is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenClientIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketClient client = null!;

        // Act
        Action act = () => _ = new ReportRunner(
            client,
            new Mock<IPullRequestAnalyzer>(MockBehavior.Strict).Object,
            new Mock<IReportPresenter>(MockBehavior.Strict).Object,
            new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object,
            new Mock<IPeopleCsvProvider>(MockBehavior.Strict).Object,
            Options.Create(CreateOptions()));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when analyzer is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenAnalyzerIsNullThrowsArgumentNullException()
    {
        // Arrange
        IPullRequestAnalyzer analyzer = null!;

        // Act
        Action act = () => _ = new ReportRunner(
            new Mock<IBitbucketClient>(MockBehavior.Strict).Object,
            analyzer,
            new Mock<IReportPresenter>(MockBehavior.Strict).Object,
            new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object,
            new Mock<IPeopleCsvProvider>(MockBehavior.Strict).Object,
            Options.Create(CreateOptions()));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when presenter is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenPresenterIsNullThrowsArgumentNullException()
    {
        // Arrange
        IReportPresenter presenter = null!;

        // Act
        Action act = () => _ = new ReportRunner(
            new Mock<IBitbucketClient>(MockBehavior.Strict).Object,
            new Mock<IPullRequestAnalyzer>(MockBehavior.Strict).Object,
            presenter,
            new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object,
            new Mock<IPeopleCsvProvider>(MockBehavior.Strict).Object,
            Options.Create(CreateOptions()));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when PDF renderer is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenPdfRendererIsNullThrowsArgumentNullException()
    {
        // Arrange
        IPdfReportRenderer pdfReportRenderer = null!;

        // Act
        Action act = () => _ = new ReportRunner(
            new Mock<IBitbucketClient>(MockBehavior.Strict).Object,
            new Mock<IPullRequestAnalyzer>(MockBehavior.Strict).Object,
            new Mock<IReportPresenter>(MockBehavior.Strict).Object,
            pdfReportRenderer,
            new Mock<IPeopleCsvProvider>(MockBehavior.Strict).Object,
            Options.Create(CreateOptions()));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when people CSV provider is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenPeopleCsvProviderIsNullThrowsArgumentNullException()
    {
        // Arrange
        IPeopleCsvProvider peopleCsvProvider = null!;

        // Act
        Action act = () => _ = new ReportRunner(
            new Mock<IBitbucketClient>(MockBehavior.Strict).Object,
            new Mock<IPullRequestAnalyzer>(MockBehavior.Strict).Object,
            new Mock<IReportPresenter>(MockBehavior.Strict).Object,
            new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object,
            peopleCsvProvider,
            Options.Create(CreateOptions()));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new ReportRunner(
            new Mock<IBitbucketClient>(MockBehavior.Strict).Object,
            new Mock<IPullRequestAnalyzer>(MockBehavior.Strict).Object,
            new Mock<IReportPresenter>(MockBehavior.Strict).Object,
            new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object,
            new Mock<IPeopleCsvProvider>(MockBehavior.Strict).Object,
            options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RunAsync orchestrates auth, filtering, analysis, and rendering")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncWhenRepositoriesAreFetchedFiltersAndAnalyzesMatchingRepositories()
    {
        // Arrange
        var options = CreateOptions(repoNameList: ["RepoA"]);
        var repoA = new Repository(new RepoName("RepoA"), new RepoSlug("repo-a"));
        var repoB = new Repository(new RepoName("RepoB"), new RepoSlug("repo-b"));
        var fetchedRepositories = new List<Repository>();
        var analyzedRepositories = new List<Repository>();
        var renderedRepositories = new List<Repository>();
        var authRequests = 0;
        var repositoryRequests = 0;
        var announceAuthCalls = 0;
        var fetchRepositoriesCalls = 0;
        var renderRepositoryTableCalls = 0;
        var renderBranchFilterInfoCalls = 0;
        var analyzeRepositoriesCalls = 0;
        var renderReportCalls = 0;
        var renderPdfCalls = 0;

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetCurrentUserAsync(It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => authRequests++)
            .ReturnsAsync(new AuthUser(new DisplayName("Tester"), new Username("tester"), new UserUuid("{tester-1}")));
        client.Setup(x => x.GetRepositoriesAsync(
                It.Is<Workspace>(workspace => workspace.Value == "workspace"),
                It.Is<Action<int>>(onPage => onPage != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => repositoryRequests++)
            .Returns((Workspace workspace, Action<int>? onPage, CancellationToken token) =>
            {
                workspace.Value.Should().Be("workspace");
                onPage?.Invoke(1);
                return ToAsyncEnumerable([repoA, repoB], token);
            });

        var analyzer = new Mock<IPullRequestAnalyzer>(MockBehavior.Strict);
        analyzer.Setup(x => x.AnalyzeAsync(
                repoA,
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback<Repository, ReportData, CancellationToken>((repository, reportData, _) =>
            {
                analyzedRepositories.Add(repository);
                reportData.Reports.Add(new PullRequestReport(
                    repository.DisplayName,
                    repository.Slug.Value,
                    "Alice",
                    "develop",
                    new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
                    mergedOn: null,
                    rejectedOn: null,
                    PullRequestState.Open,
                    new PullRequestId(1),
                    comments: 0,
                    firstReactionOn: null));
            })
            .Returns(Task.CompletedTask);

        var presenter = new Mock<IReportPresenter>(MockBehavior.Strict);
        presenter.Setup(x => x.AnnounceAuthAsync(
                It.Is<Func<CancellationToken, Task<AuthUser>>>(fetchUser => fetchUser != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => announceAuthCalls++)
            .Returns(async (Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken token) =>
            {
                _ = await fetchUser(token);
            });
        presenter.Setup(x => x.FetchRepositoriesAsync(
                It.Is<Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>>>(fetch => fetch != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => fetchRepositoriesCalls++)
            .Returns(async (Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetch, CancellationToken token) =>
            {
                var result = new List<Repository>();
                await foreach (var repository in fetch(_ => { }, token))
                {
                    fetchedRepositories.Add(repository);
                    result.Add(repository);
                }

                return result;
            });
        presenter.Setup(x => x.RenderRepositoryTable(
                It.Is<IReadOnlyCollection<Repository>>(repositories =>
                    repositories.Count == 1 &&
                    repositories.Single().Slug.Value == "repo-a"),
                It.Is<ReportParameters>(parameters =>
                    parameters.Workspace.Value == "workspace")))
            .Callback<IReadOnlyCollection<Repository>, ReportParameters>((repositories, _) =>
            {
                renderRepositoryTableCalls++;
                renderedRepositories.AddRange(repositories);
            });
        presenter.Setup(x => x.RenderBranchFilterInfo(
                It.Is<ReportParameters>(parameters => parameters.Workspace.Value == "workspace")))
            .Callback(() => renderBranchFilterInfoCalls++);
        presenter.Setup(x => x.AnalyzeRepositoriesAsync(
                It.Is<IReadOnlyList<Repository>>(repositories =>
                    repositories.Count == 1 &&
                    repositories.Single().Slug.Value == "repo-a"),
                It.Is<Func<Repository, CancellationToken, Task>>(analyze => analyze != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => analyzeRepositoriesCalls++)
            .Returns(async (IReadOnlyList<Repository> repositories, Func<Repository, CancellationToken, Task> analyze, CancellationToken token) =>
            {
                foreach (var repository in repositories)
                {
                    await analyze(repository, token);
                }
            });
        presenter.Setup(x => x.RenderReport(
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace")))
            .Callback(() => renderReportCalls++);

        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        pdfRenderer.Setup(x => x.RenderReportAsync(
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => renderPdfCalls++)
            .Returns(Task.CompletedTask);

        var peopleCsvProvider = new Mock<IPeopleCsvProvider>(MockBehavior.Strict);
        peopleCsvProvider.Setup(x => x.GetPeopleByDisplayNameAsync(It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new Dictionary<DisplayName, PersonCsvRow>());

        var runner = new ReportRunner(
            client.Object,
            analyzer.Object,
            presenter.Object,
            pdfRenderer.Object,
            peopleCsvProvider.Object,
            Options.Create(options));

        // Act
        await runner.RunAsync(cancellationToken);

        // Assert
        fetchedRepositories.Select(repository => repository.Slug.Value).Should().Equal("repo-a", "repo-b");
        renderedRepositories.Select(repository => repository.Slug.Value).Should().Equal("repo-a");
        analyzedRepositories.Select(repository => repository.Slug.Value).Should().Equal("repo-a");
        authRequests.Should().Be(1);
        repositoryRequests.Should().Be(1);
        announceAuthCalls.Should().Be(1);
        fetchRepositoriesCalls.Should().Be(1);
        renderRepositoryTableCalls.Should().Be(1);
        renderBranchFilterInfoCalls.Should().Be(1);
        analyzeRepositoriesCalls.Should().Be(1);
        renderReportCalls.Should().Be(1);
        renderPdfCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RunAsync still renders outputs when no repositories match filter")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncWhenNoRepositoriesMatchStillRendersReportAndPdf()
    {
        // Arrange
        var options = CreateOptions(repoNameList: ["NotFound"]);
        var repository = new Repository(new RepoName("RepoA"), new RepoSlug("repo-a"));
        var analyzedAnyRepository = false;
        var authRequests = 0;
        var repositoryRequests = 0;
        var announceAuthCalls = 0;
        var fetchRepositoriesCalls = 0;
        var renderRepositoryTableCalls = 0;
        var renderBranchFilterInfoCalls = 0;
        var analyzeRepositoriesCalls = 0;
        var renderReportCalls = 0;
        var renderPdfCalls = 0;

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetCurrentUserAsync(It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => authRequests++)
            .ReturnsAsync(new AuthUser(new DisplayName("Tester"), new Username("tester"), new UserUuid("{tester-1}")));
        client.Setup(x => x.GetRepositoriesAsync(
                It.Is<Workspace>(workspace => workspace.Value == "workspace"),
                It.Is<Action<int>>(onPage => onPage != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => repositoryRequests++)
            .Returns((Workspace _, Action<int>? __, CancellationToken token) =>
                ToAsyncEnumerable([repository], token));

        var analyzer = new Mock<IPullRequestAnalyzer>(MockBehavior.Strict);
        analyzer.Setup(x => x.AnalyzeAsync(
                It.Is<Repository>(value => value.Slug.Value == "repo-a"),
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => analyzedAnyRepository = true)
            .Returns(Task.CompletedTask);

        var presenter = new Mock<IReportPresenter>(MockBehavior.Strict);
        presenter.Setup(x => x.AnnounceAuthAsync(
                It.Is<Func<CancellationToken, Task<AuthUser>>>(fetchUser => fetchUser != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => announceAuthCalls++)
            .Returns(async (Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken token) =>
            {
                _ = await fetchUser(token);
            });
        presenter.Setup(x => x.FetchRepositoriesAsync(
                It.Is<Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>>>(fetch => fetch != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => fetchRepositoriesCalls++)
            .Returns((Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetch, CancellationToken token) =>
                ReadAllAsync(fetch(_ => { }, token)));
        presenter.Setup(x => x.RenderRepositoryTable(
                It.Is<IReadOnlyCollection<Repository>>(repositories => repositories.Count == 0),
                It.Is<ReportParameters>(parameters => parameters.Workspace.Value == "workspace")))
            .Callback(() => renderRepositoryTableCalls++);
        presenter.Setup(x => x.RenderBranchFilterInfo(
                It.Is<ReportParameters>(parameters => parameters.Workspace.Value == "workspace")))
            .Callback(() => renderBranchFilterInfoCalls++);
        presenter.Setup(x => x.AnalyzeRepositoriesAsync(
                It.Is<IReadOnlyList<Repository>>(repositories => repositories.Count == 0),
                It.Is<Func<Repository, CancellationToken, Task>>(analyze => analyze != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => analyzeRepositoriesCalls++)
            .Returns(Task.CompletedTask);
        presenter.Setup(x => x.RenderReport(
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace")))
            .Callback(() => renderReportCalls++);

        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        pdfRenderer.Setup(x => x.RenderReportAsync(
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback(() => renderPdfCalls++)
            .Returns(Task.CompletedTask);

        var peopleCsvProvider = new Mock<IPeopleCsvProvider>(MockBehavior.Strict);
        peopleCsvProvider.Setup(x => x.GetPeopleByDisplayNameAsync(It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new Dictionary<DisplayName, PersonCsvRow>());

        var runner = new ReportRunner(
            client.Object,
            analyzer.Object,
            presenter.Object,
            pdfRenderer.Object,
            peopleCsvProvider.Object,
            Options.Create(options));

        // Act
        await runner.RunAsync(cancellationToken);

        // Assert
        analyzedAnyRepository.Should().BeFalse();
        authRequests.Should().Be(1);
        repositoryRequests.Should().Be(1);
        announceAuthCalls.Should().Be(1);
        fetchRepositoriesCalls.Should().Be(1);
        renderRepositoryTableCalls.Should().Be(1);
        renderBranchFilterInfoCalls.Should().Be(1);
        analyzeRepositoriesCalls.Should().Be(1);
        renderReportCalls.Should().Be(1);
        renderPdfCalls.Should().Be(1);
    }

    [Fact(DisplayName = "RunAsync enriches developer stats from people CSV when configured")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncWhenPeopleCsvPathIsConfiguredEnrichesDeveloperStatsByExactName()
    {
        // Arrange
        var options = CreateOptions(repoNameList: ["RepoA"], peopleCsvPath: "people.csv");
        var repository = new Repository(new RepoName("RepoA"), new RepoSlug("repo-a"));
        ReportData? capturedReportData = null;

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetCurrentUserAsync(It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new AuthUser(new DisplayName("Tester"), new Username("tester"), new UserUuid("{tester-1}")));
        client.Setup(x => x.GetRepositoriesAsync(
                It.Is<Workspace>(workspace => workspace.Value == "workspace"),
                It.Is<Action<int>>(onPage => onPage != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns((Workspace _, Action<int>? __, CancellationToken token) =>
                ToAsyncEnumerable([repository], token));

        var analyzer = new Mock<IPullRequestAnalyzer>(MockBehavior.Strict);
        analyzer.Setup(x => x.AnalyzeAsync(
                repository,
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback<Repository, ReportData, CancellationToken>((_, reportData, _) =>
            {
                _ = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("{alice-1}"), new DisplayName("Alice")));
                _ = reportData.GetOrAddDeveloper(new DeveloperIdentity(new UserUuid("{bob-1}"), new DisplayName("Bob")));
            })
            .Returns(Task.CompletedTask);

        var presenter = new Mock<IReportPresenter>(MockBehavior.Strict);
        presenter.Setup(x => x.AnnounceAuthAsync(
                It.Is<Func<CancellationToken, Task<AuthUser>>>(fetchUser => fetchUser != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(async (Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken token) =>
            {
                _ = await fetchUser(token);
            });
        presenter.Setup(x => x.FetchRepositoriesAsync(
                It.Is<Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>>>(fetch => fetch != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns((Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetch, CancellationToken token) =>
                ReadAllAsync(fetch(_ => { }, token)));
        presenter.Setup(x => x.RenderRepositoryTable(
            It.Is<IReadOnlyCollection<Repository>>(repositories =>
                repositories.Count == 1 &&
                repositories.Single().Slug.Value == "repo-a"),
            It.Is<ReportParameters>(parameters => parameters.Workspace.Value == "workspace")));
        presenter.Setup(x => x.RenderBranchFilterInfo(
            It.Is<ReportParameters>(parameters => parameters.Workspace.Value == "workspace")));
        presenter.Setup(x => x.AnalyzeRepositoriesAsync(
                It.Is<IReadOnlyList<Repository>>(repositories =>
                    repositories.Count == 1 &&
                    repositories.Single().Slug.Value == "repo-a"),
                It.Is<Func<Repository, CancellationToken, Task>>(analyze => analyze != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(async (IReadOnlyList<Repository> repositories, Func<Repository, CancellationToken, Task> analyze, CancellationToken token) =>
            {
                foreach (var repo in repositories)
                {
                    await analyze(repo, token);
                }
            });
        presenter.Setup(x => x.RenderReport(
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace")))
            .Callback<ReportData>(reportData => capturedReportData = reportData);

        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        pdfRenderer.Setup(x => x.RenderReportAsync(
                It.Is<ReportData>(data => data.Parameters.Workspace.Value == "workspace"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(Task.CompletedTask);

        var peopleCsvProvider = new Mock<IPeopleCsvProvider>(MockBehavior.Strict);
        peopleCsvProvider.Setup(x => x.GetPeopleByDisplayNameAsync(It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new Dictionary<DisplayName, PersonCsvRow>
            {
                [new DisplayName("Alice")] = new PersonCsvRow("Senior", "Core Platform")
            });

        var runner = new ReportRunner(
            client.Object,
            analyzer.Object,
            presenter.Object,
            pdfRenderer.Object,
            peopleCsvProvider.Object,
            Options.Create(options));

        // Act
        await runner.RunAsync(cancellationToken);

        // Assert
        capturedReportData.Should().NotBeNull();
        var statsByName = capturedReportData!.DeveloperStats.Values
            .ToDictionary(stat => stat.DisplayName.Value, StringComparer.Ordinal);
        statsByName["Alice"].Grade.Should().Be("Senior");
        statsByName["Alice"].Department.Should().Be("Core Platform");
        statsByName["Bob"].Grade.Should().Be(DeveloperStats.NOT_AVAILABLE);
        statsByName["Bob"].Department.Should().Be(DeveloperStats.NOT_AVAILABLE);
    }

    [Fact(DisplayName = "RunAsync loads people CSV before analysis when team filter is configured")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncWhenTeamFilterConfiguredMakesPeopleAvailableDuringAnalysis()
    {
        // Arrange
        var options = CreateOptions(repoNameList: ["RepoA"], peopleCsvPath: "people.csv", teamFilter: "Core");
        var repository = new Repository(new RepoName("RepoA"), new RepoSlug("repo-a"));
        var peopleLoadedBeforeAnalyze = false;

        var client = new Mock<IBitbucketClient>(MockBehavior.Strict);
        client.Setup(x => x.GetCurrentUserAsync(It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new AuthUser(new DisplayName("Tester"), new Username("tester"), new UserUuid("{tester-1}")));
        client.Setup(x => x.GetRepositoriesAsync(
                It.Is<Workspace>(workspace => workspace.Value == "workspace"),
                It.Is<Action<int>>(onPage => onPage != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns((Workspace _, Action<int>? __, CancellationToken token) =>
                ToAsyncEnumerable([repository], token));

        var analyzer = new Mock<IPullRequestAnalyzer>(MockBehavior.Strict);
        analyzer.Setup(x => x.AnalyzeAsync(
                repository,
                It.Is<ReportData>(data => data.Parameters.TeamFilter == "Core"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Callback<Repository, ReportData, CancellationToken>((_, reportData, _) =>
            {
                peopleLoadedBeforeAnalyze = reportData.IsDeveloperIncluded(
                    new DeveloperIdentity(null, new DisplayName("Alice")));
            })
            .Returns(Task.CompletedTask);

        var presenter = new Mock<IReportPresenter>(MockBehavior.Strict);
        presenter.Setup(x => x.AnnounceAuthAsync(
                It.Is<Func<CancellationToken, Task<AuthUser>>>(fetchUser => fetchUser != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(async (Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken token) =>
            {
                _ = await fetchUser(token);
            });
        presenter.Setup(x => x.FetchRepositoriesAsync(
                It.Is<Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>>>(fetch => fetch != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns((Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetch, CancellationToken token) =>
                ReadAllAsync(fetch(_ => { }, token)));
        presenter.Setup(x => x.RenderRepositoryTable(
            It.Is<IReadOnlyCollection<Repository>>(repositories =>
                repositories.Count == 1 &&
                repositories.Single().Slug.Value == "repo-a"),
            It.Is<ReportParameters>(parameters => parameters.Workspace.Value == "workspace")));
        presenter.Setup(x => x.RenderBranchFilterInfo(
            It.Is<ReportParameters>(parameters => parameters.Workspace.Value == "workspace")));
        presenter.Setup(x => x.AnalyzeRepositoriesAsync(
                It.Is<IReadOnlyList<Repository>>(repositories =>
                    repositories.Count == 1 &&
                    repositories.Single().Slug.Value == "repo-a"),
                It.Is<Func<Repository, CancellationToken, Task>>(analyze => analyze != null),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(async (IReadOnlyList<Repository> repositories, Func<Repository, CancellationToken, Task> analyze, CancellationToken token) =>
            {
                foreach (var repo in repositories)
                {
                    await analyze(repo, token);
                }
            });
        presenter.Setup(x => x.RenderReport(
                It.Is<ReportData>(data => data.Parameters.TeamFilter == "Core")));

        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        pdfRenderer.Setup(x => x.RenderReportAsync(
                It.Is<ReportData>(data => data.Parameters.TeamFilter == "Core"),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .Returns(Task.CompletedTask);

        var peopleCsvProvider = new Mock<IPeopleCsvProvider>(MockBehavior.Strict);
        peopleCsvProvider.Setup(x => x.GetPeopleByDisplayNameAsync(It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(new Dictionary<DisplayName, PersonCsvRow>
            {
                [new DisplayName("Alice")] = new PersonCsvRow("Senior", "Core")
            });

        var runner = new ReportRunner(
            client.Object,
            analyzer.Object,
            presenter.Object,
            pdfRenderer.Object,
            peopleCsvProvider.Object,
            Options.Create(options));

        // Act
        await runner.RunAsync(cancellationToken);

        // Assert
        peopleLoadedBeforeAnalyze.Should().BeTrue();
    }

    private static BitbucketOptions CreateOptions(
        IReadOnlyList<string>? repoNameList = null,
        string? peopleCsvPath = null,
        string? teamFilter = null)
    {
        return new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 25,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = repoNameList?.ToArray() ?? [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            PeopleCsvPath = peopleCsvPath,
            TeamFilter = teamFilter,
            Pdf = new PdfOptions()
        };
    }

    private static async IAsyncEnumerable<Repository> ToAsyncEnumerable(
        IReadOnlyList<Repository> values,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return value;
            await Task.Yield();
        }
    }

    private static async Task<IReadOnlyList<Repository>> ReadAllAsync(IAsyncEnumerable<Repository> source)
    {
        var result = new List<Repository>();
        await foreach (var repository in source)
        {
            result.Add(repository);
        }

        return result;
    }
}


using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

using FluentAssertions;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreRepositoryAnalysisPresenterTests
{
    [Fact]
    public async Task RunsApplicationCallbackAndForwardsProgress()
    {
        var presenter = new SpectreRepositoryAnalysisPresenter();
        var executed = false;
        await TestConsoleRunner.RunAsync(async _ => await presenter.RunWithRepositoryProgressAsync(2,
            (progress, _) =>
            {
                executed = true;
                progress(new RepositoryAnalysisProgress("Repo A", 0, 1, 2));
                progress(new RepositoryAnalysisProgress(null, 1, 0, 2));
                return Task.CompletedTask;
            }, CancellationToken.None));
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task RejectsMissingApplicationCallback()
    {
        var presenter = new SpectreRepositoryAnalysisPresenter();
        Func<Task> run = () => presenter.RunWithRepositoryProgressAsync(1, null!, CancellationToken.None);
        await run.Should().ThrowAsync<ArgumentNullException>();
    }
}

using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;
using BBDevPulse.Presentation.Html;

using Microsoft.Extensions.Options;

using Moq;

namespace BBDevPulse.Tests.Presentation.Html;

public sealed class HtmlReportRendererTests
{
    [Fact(DisplayName = "RenderReport returns without saving when HTML generation is disabled")]
    [Trait("Category", "Unit")]
    public async Task RenderReportAsyncWhenHtmlIsDisabledDoesNotPersistDocument()
    {
        // Arrange
        var fileStore = new Mock<IHtmlReportFileStore>(MockBehavior.Strict);
        var composer = new Mock<IHtmlContentComposer>(MockBehavior.Strict);
        var launcher = new Mock<IHtmlReportLauncher>(MockBehavior.Strict);
        var renderer = new HtmlReportRenderer(
            Options.Create(CreateOptions(new HtmlOptions { Enabled = false })),
            fileStore.Object,
            composer.Object,
            launcher.Object);

        // Act
        await renderer.RenderReportAsync(new ReportData(CreateParameters()));
    }

    [Fact(DisplayName = "RenderReport saves HTML and opens browser when configured")]
    [Trait("Category", "Unit")]
    public async Task RenderReportAsyncWhenOpenInBrowserEnabledSavesAndLaunches()
    {
        // Arrange
        var htmlOptions = new HtmlOptions
        {
            Enabled = true,
            OutputPath = "bbdevpulse-report.html",
            OpenInBrowser = true
        };
        var fileStore = new Mock<IHtmlReportFileStore>(MockBehavior.Strict);
        string? savedPath = null;
        fileStore.Setup(x => x.SaveAsync(
                It.Is<string>(path => Path.GetFileName(path).StartsWith("bbdevpulse-report_", StringComparison.OrdinalIgnoreCase)),
                "<html>report</html>",
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((path, _, _) => savedPath = path)
            .Returns(Task.CompletedTask);

        var composer = new Mock<IHtmlContentComposer>(MockBehavior.Strict);
        composer.Setup(x => x.Compose(It.IsAny<ReportData>()))
            .Returns("<html>report</html>");

        var launcher = new Mock<IHtmlReportLauncher>(MockBehavior.Strict);
        launcher.Setup(x => x.Open(It.Is<string>(path => path == savedPath)));

        var renderer = new HtmlReportRenderer(
            Options.Create(CreateOptions(htmlOptions)),
            fileStore.Object,
            composer.Object,
            launcher.Object);

        // Act
        await renderer.RenderReportAsync(new ReportData(CreateParameters()));

        // Assert
        savedPath.Should().NotBeNullOrWhiteSpace();
    }

    private static BitbucketOptions CreateOptions(HtmlOptions html) =>
        new()
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 25,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Html = html,
            Pdf = new PdfOptions()
        };

    private static ReportParameters CreateParameters() =>
        new(
            new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero),
            new Workspace("workspace"),
            new RepoNameFilter(string.Empty),
            repoNameList: [],
            RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode.CreatedOnOnly,
            branchNameList: []);
}

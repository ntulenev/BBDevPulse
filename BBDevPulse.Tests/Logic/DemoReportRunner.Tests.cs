using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.Logic;
using BBDevPulse.Models;

using Moq;

namespace BBDevPulse.Tests.Logic;

public sealed class DemoReportRunnerTests
{
    [Theory(DisplayName = "Demo runner waits for confirmation before rendering synthetic data")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsyncRespectsConfirmation(bool confirmed)
    {
        var presented = false;
        var presenter = new Mock<IReportPresenter>(MockBehavior.Strict);
        presenter.Setup(x => x.SelectTeamFilter(
                It.Is<IReadOnlyList<TeamFilterOption>>(teams => teams.Count == 3 &&
                    teams.Any(team => team.Name == "Platform" && team.Members.Count == 2)), 4))
            .Returns((string?)null);
        presenter.Setup(x => x.RenderDemoSettings(
                It.Is<ReportParameters>(parameters => parameters.IsDemo),
                It.Is<string>(name => name.StartsWith("bbdevpulse-demo-report_", StringComparison.Ordinal) &&
                    name.EndsWith(".html", StringComparison.Ordinal) && Path.GetFileName(name) == name),
                It.Is<string>(name => name.StartsWith("bbdevpulse-demo-report_", StringComparison.Ordinal) &&
                    name.EndsWith(".pdf", StringComparison.Ordinal) && Path.GetFileName(name) == name)))
            .Callback(() => presented = true);
        presenter.Setup(x => x.ConfirmDemoStart()).Returns(() =>
        {
            presented.Should().BeTrue();
            return confirmed;
        });
        if (confirmed)
        {
            presenter.Setup(x => x.RenderReport(It.Is<ReportData>(report =>
                report.Parameters.IsDemo && report.Reports.Count == 12)));
        }

        var html = new Mock<IHtmlReportRenderer>(MockBehavior.Strict);
        var pdf = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        if (confirmed)
        {
            html.Setup(x => x.RenderReportAsync(It.Is<ReportData>(report => report.Parameters.IsDemo),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            pdf.Setup(x => x.RenderReportAsync(It.Is<ReportData>(report => report.Parameters.IsDemo),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        var runner = new DemoReportRunner(presenter.Object, new ReportFilePublisher(html.Object, pdf.Object));
        await runner.RunAsync(CancellationToken.None);

        presenter.Verify(x => x.SelectTeamFilter(It.IsAny<IReadOnlyList<TeamFilterOption>>(), 4), Times.Once);
        presenter.Verify(x => x.ConfirmDemoStart(), Times.Once);
        if (confirmed)
        {
            html.Verify(x => x.RenderReportAsync(It.IsAny<ReportData>(), It.IsAny<CancellationToken>()), Times.Once);
            pdf.Verify(x => x.RenderReportAsync(It.IsAny<ReportData>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            html.VerifyNoOtherCalls();
            pdf.VerifyNoOtherCalls();
        }
    }

    [Fact(DisplayName = "Demo runner passes selected synthetic team to report and settings")]
    public async Task RunAsyncAppliesSelectedTeam()
    {
        var presenter = new Mock<IReportPresenter>(MockBehavior.Strict);
        presenter.Setup(x => x.SelectTeamFilter(It.IsAny<IReadOnlyList<TeamFilterOption>>(), 4))
            .Returns("Platform");
        presenter.Setup(x => x.RenderDemoSettings(
            It.Is<ReportParameters>(parameters => parameters.TeamFilter == "Platform"),
            It.Is<string>(name => name.StartsWith("platform-bbdevpulse-demo-report_", StringComparison.Ordinal)),
            It.Is<string>(name => name.StartsWith("platform-bbdevpulse-demo-report_", StringComparison.Ordinal))));
        presenter.Setup(x => x.ConfirmDemoStart()).Returns(true);
        presenter.Setup(x => x.RenderReport(It.Is<ReportData>(report =>
            report.Parameters.TeamFilter == "Platform" && report.DeveloperStats.Count == 2)));

        var html = new Mock<IHtmlReportRenderer>(MockBehavior.Strict);
        html.Setup(x => x.RenderReportAsync(It.Is<ReportData>(report => report.Parameters.TeamFilter == "Platform"),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var pdf = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        pdf.Setup(x => x.RenderReportAsync(It.Is<ReportData>(report => report.Parameters.TeamFilter == "Platform"),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await new DemoReportRunner(presenter.Object, new ReportFilePublisher(html.Object, pdf.Object)).RunAsync(CancellationToken.None);

        presenter.VerifyAll();
        html.VerifyAll();
        pdf.VerifyAll();
    }
}

using FluentAssertions;

using BBDevPulse.Logic;
using BBDevPulse.Models;

namespace BBDevPulse.Tests.Logic;

public sealed class DemoReportDataTests
{
    [Fact(DisplayName = "Demo report contains diverse synthetic metrics and developer details")]
    public void CreateBuildsSyntheticReport()
    {
        var report = DemoReportData.Create();

        report.Parameters.IsDemo.Should().BeTrue();
        report.Parameters.Workspace.Value.Should().Be("demo-workspace");
        report.Reports.Should().HaveCount(12);
        report.Reports.Should().Contain(item => item.State == PullRequestState.Merged);
        report.Reports.Should().Contain(item => item.State == PullRequestState.Open);
        report.Reports.Should().Contain(item => item.State == PullRequestState.Declined);
        report.DeveloperStats.Should().HaveCount(4);
        report.DeveloperStats.Values.Should().OnlyContain(item => item.Department != DeveloperStats.NOT_AVAILABLE);
        report.DeveloperStats.Values.Sum(item => item.PrsOpenedSince).Should().Be(report.Reports.Count);
        report.DeveloperStats.Values.Sum(item => item.CommentActivities.Count).Should().Be(report.Reports.Sum(item => item.Comments));
        report.DeveloperStats.Values.Sum(item => item.CommitActivities.Count).Should().BeGreaterThan(0);
    }
}

using FluentAssertions;

using BBDevPulse.Logic;
using BBDevPulse.Models;

namespace BBDevPulse.Tests.Logic;

public sealed class DemoReportDataTests
{
    [Fact(DisplayName = "Demo report contains diverse synthetic metrics and developer details")]
    public async Task CreateBuildsSyntheticReport()
    {
        var people = await DemoPeopleCsv.LoadAsync(CancellationToken.None);
        var report = DemoReportData.Create(people);

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

    [Fact(DisplayName = "Demo team filter uses synthetic CSV people and keeps reviewer-only activity")]
    public async Task CreateFiltersSyntheticPeopleAndPullRequests()
    {
        var people = await DemoPeopleCsv.LoadAsync(CancellationToken.None);
        var teams = TeamFilterCatalog.Create(people);
        teams.Should().ContainSingle(team => team.Name == "Platform" && team.Members.Count == 2 &&
            team.Members.Any(member => member.Name == "Avery Stone" && member.Grade == "Senior") &&
            team.Members.Any(member => member.Name == "Morgan Vale" && member.Grade == "Lead"));

        var report = DemoReportData.Create(people, "Platform");

        report.Parameters.TeamFilter.Should().Be("Platform");
        report.DeveloperStats.Values.Select(item => item.DisplayName.Value)
            .Should().BeEquivalentTo(["Avery Stone", "Morgan Vale"]);
        report.DeveloperStats.Values.Should().OnlyContain(item => item.Department == "Platform");
        report.Reports.Should().OnlyContain(item =>
            item.IncludeInMetrics == (item.Author == "Avery Stone" || item.Author == "Morgan Vale"));
        report.Reports.Should().Contain(item => !item.IncludeInMetrics && item.Author == "Taylor Quinn");
        report.DeveloperStats.Values.Sum(item => item.PrsOpenedSince)
            .Should().Be(report.Reports.Count(item => item.IncludeInMetrics));
    }
}

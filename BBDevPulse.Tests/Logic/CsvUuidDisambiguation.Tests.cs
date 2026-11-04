using System.Text;
using BBDevPulse.Logic;
using BBDevPulse.Math;
using BBDevPulse.Models;
using BBDevPulse.Presentation.Formatters;
using BBDevPulse.Presentation.Html;
using FluentAssertions;

namespace BBDevPulse.Tests.Logic;

public sealed class CsvUuidDisambiguationTests
{
    private static readonly DeveloperIdentity First = new(new UserUuid("{a}"), new DisplayName("Alex Kim"));
    private static readonly DeveloperIdentity Second = new(new UserUuid("{b}"), new DisplayName("Alex Kim"));

    [Fact]
    public async Task DuplicateNamesWithUuidsResolveTeamAndEnrichmentIndependently()
    {
        var people = await ReadCsv("Name;Grade;Department;UUID\nAlex Kim;Senior;Platform;A\nAlex Kim;Junior;Other;{b}\n");
        var data = CreateReport(people, "Platform");
        data.IsDeveloperIncluded(First).Should().BeTrue();
        data.IsDeveloperIncluded(Second).Should().BeFalse();
        data.TryGetPerson(First, out var firstPerson).Should().BeTrue();
        data.TryGetPerson(Second, out var secondPerson).Should().BeTrue();
        firstPerson.Grade.Should().Be("Senior");
        secondPerson.Grade.Should().Be("Junior");
        TeamFilterCatalog.Create(people).Select(x => x.Name).Should().BeEquivalentTo("Platform", "Other");
        var notice = data.GetIdentityCollisionNotices().Should().ContainSingle().Subject;
        notice.IsError.Should().BeFalse();
        notice.Message.Should().Contain("with UUIDs");
        var html = Render(data);
        html.Should().Contain("#d97706").And.Contain(notice.Message).And.Contain("Alex Kim: {a}, {b}");
    }

    [Fact]
    public async Task NameInSelectedTeamWithoutUuidWarnsRedAndIncludesBoth()
    {
        var people = await ReadCsv("Name;Grade;Department\nAlex Kim;Senior;Platform\n");
        var data = CreateReport(people, "Platform");
        data.IsDeveloperIncluded(First).Should().BeTrue();
        data.IsDeveloperIncluded(Second).Should().BeTrue();
        var notice = data.GetIdentityCollisionNotices().Should().ContainSingle().Subject;
        notice.IsError.Should().BeTrue();
        notice.Message.Should().Contain("without a UUID");
        Render(data).Should().Contain("#b91c1c").And.Contain(notice.Message);
    }

    [Fact]
    public async Task NameOutsideSelectedTeamWarnsYellowAndExcludesBoth()
    {
        var people = await ReadCsv("Name;Grade;Department;UUID\nAlex Kim;Senior;Other;\n");
        var data = CreateReport(people, "Platform");
        data.IsDeveloperIncluded(First).Should().BeFalse();
        data.IsDeveloperIncluded(Second).Should().BeFalse();
        var notice = data.GetIdentityCollisionNotices().Should().ContainSingle().Subject;
        notice.IsError.Should().BeFalse();
        notice.Message.Should().Contain("not in the selected team");
        Render(data).Should().Contain("#d97706").And.Contain(notice.Message);
    }

    [Fact]
    public async Task ExactUuidTakesPrecedenceOverNameOnlyFallback()
    {
        var people = await ReadCsv("Name;Grade;Department;UUID\nAlex Kim;Senior;Platform;\nAlex Kim;Junior;Other;b\n");
        var data = CreateReport(people, "Platform");
        data.IsDeveloperIncluded(First).Should().BeTrue();
        data.IsDeveloperIncluded(Second).Should().BeFalse();
        data.GetIdentityCollisionNotices().Single().IsError.Should().BeTrue();
    }

    [Fact]
    public async Task UuidDoesNotMatchAnUnrelatedUserWithTheSameName()
    {
        var people = await ReadCsv("Name;Grade;Department;UUID\nAlex Kim;Senior;Platform;a\n");
        var data = CreateReport(people, "Platform");
        data.IsDeveloperIncluded(First).Should().BeTrue();
        data.IsDeveloperIncluded(Second).Should().BeFalse();
        data.TryGetPerson(Second, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("Alex Kim;Senior;Import;\nAlex Kim;Senior;Export;\nAlex Kim;Senior;Export;\n", false)]
    [InlineData("Alex Kim;Senior;Import;\nAlex Kim;Senior;Export;UUID1\nAlex Kim;Senior;Export;\n", false)]
    [InlineData("Alex Kim;Senior;Import;UUID1\nAlex Kim;Senior;Export;UUID1\nAlex Kim;Senior;Export;\n", false)]
    [InlineData("Alex Kim;Senior;Import;\nAlex Kim;Senior;Export;UUID1\nAlex Kim;Senior;Export;UUID1\n", false)]
    [InlineData("Alex Kim;Senior;Import;\nAlex Kim;Senior;Export;UUID1\nAlex Kim;Senior;Export;UUID2\n", true)]
    [InlineData("Alex Kim;Senior;Import;UUID2\nAlex Kim;Senior;Export;UUID1\nAlex Kim;Senior;Export;UUID2\n", false)]
    [InlineData("Alex Kim;Senior;Import;UUID3\nAlex Kim;Senior;Export;UUID1\nAlex Kim;Senior;Export;UUID2\n", true)]
    public async Task ValidatesRequestedDuplicateCases(string rows, bool valid)
    {
        Func<Task> read = async () => { _ = await ReadCsv("Name;Grade;Department;UUID\n" + rows); };
        if (valid)
        {
            await read.Should().NotThrowAsync();
            var people = await ReadCsv("Name;Grade;Department;UUID\n" + rows);
            people.Single().Value.Rows.Should().HaveCount(3);
        }
        else
        {
            (await read.Should().ThrowAsync<PeopleCsvFormatException>()).Which.Message.Should().Contain("lines");
        }
    }

    [Fact]
    public async Task DuplicateUuidIsRejectedAcrossDifferentNamesAndNormalizedBraces()
    {
        Func<Task> read = async () => { _ = await ReadCsv("Name;Grade;Department;UUID\nAlice;Senior;Core;{AB}\nBob;Junior;Other;ab\n"); };
        (await read.Should().ThrowAsync<PeopleCsvFormatException>()).Which.Message.Should().Contain("lines 2 and 3");
    }

    [Fact]
    public async Task SameNameWithoutUuidsAcrossDifferentTeamsRetainsBothAndWarns()
    {
        var people = await ReadCsv("Name;Grade;Department\nAlex Kim;Senior;Import\nAlex Kim;Junior;Export\n");
        people.Single().Value.Rows.Should().HaveCount(2);
        var import = CreateReport(people, "Import");
        var export = CreateReport(people, "Export");
        import.IsDeveloperIncluded(First).Should().BeTrue();
        export.IsDeveloperIncluded(First).Should().BeTrue();
        import.TryGetPerson(First, out _).Should().BeFalse();
        import.GetIdentityCollisionNotices().Single().IsError.Should().BeTrue();
        export.GetIdentityCollisionNotices().Single().IsError.Should().BeTrue();
    }

    [Fact]
    public async Task DuplicateNamesAndTeamsCompareIgnoringCase()
    {
        Func<Task> read = async () => { _ = await ReadCsv("Name;Grade;Department\nAlex Kim;Senior;Export\naLEX KIM;Junior;export\n"); };
        (await read.Should().ThrowAsync<PeopleCsvFormatException>()).Which.Message.Should().Contain("Every such entry must have a unique UUID");
    }

    private static async Task<Dictionary<DisplayName, PersonCsvRow>> ReadCsv(string csv)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await PeopleCsvProvider.ReadPeopleAsync(stream, CancellationToken.None);
    }

    private static ReportData CreateReport(Dictionary<DisplayName, PersonCsvRow> people, string team)
    {
        var parameters = new ReportParameters(DateTimeOffset.UnixEpoch, new Workspace("ws"), new RepoNameFilter(""), [],
            RepoSearchMode.SearchByFilter, PrTimeFilterMode.CreatedOnOnly, [], teamFilter: team);
        var data = new ReportData(parameters, people);
        data.ObserveDeveloperIdentity(First);
        data.ObserveDeveloperIdentity(Second);
        return data;
    }

    private static string Render(ReportData data) => new HtmlContentComposer(new DateDiffFormatter(), new StatisticsCalculator()).Compose(data);
}

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
    public async Task DifferentNameStillMatchesUuidAndIsReported()
    {
        var people = await ReadCsv("Name;Grade;Department;UUID\nAlex CSV;Senior;Platform;A\n");
        var data = CreateReport(people, "Platform");
        data.IsDeveloperIncluded(First).Should().BeTrue();
        data.TryGetPerson(First, out var person).Should().BeTrue();
        person.Grade.Should().Be("Senior");
        data.GetNameMismatches().Should().ContainSingle().Which.Should().Be(("{a}", "Alex Kim", "Alex CSV"));
        Render(data).Should().Contain("Bitbucket: Alex Kim; CSV: Alex CSV");
    }

    [Theory]
    [InlineData("Alex Kim", false)]
    [InlineData("alex Kim", true)]
    [InlineData("Alex Kimm", true)]
    public async Task NameComparisonDoesNotAffectUuidFiltering(string bitbucketName, bool mismatch)
    {
        var people = await ReadCsv("Name;Grade;Department;UUID\nAlex Kim;Senior;Platform;{a}\n");
        var parameters = new ReportParameters(DateTimeOffset.UnixEpoch, new Workspace("ws"), new RepoNameFilter(""), [],
            RepoSearchMode.SearchByFilter, PrTimeFilterMode.CreatedOnOnly, [], teamFilter: "Platform");
        var accumulator = new ReportAccumulator(parameters, people);
        var identity = new DeveloperIdentity(new UserUuid("A"), new DisplayName(bitbucketName));
        accumulator.IsDeveloperIncluded(identity).Should().BeTrue();
        accumulator.RegisterDeveloper(identity);
        accumulator.EnrichDevelopers();
        var report = accumulator.Build();
        report.DeveloperStats.Values.Should().ContainSingle().Which.Grade.Should().Be("Senior");
        report.GetNameMismatches().Count.Should().Be(mismatch ? 1 : 0);
    }

    [Fact]
    public async Task AllDistinctObservedNamesSurvivePublicationWithoutDuplicateUuidVariants()
    {
        var people = await ReadCsv("Name;Grade;Department;UUID\nAlex Kim;Senior;Platform;a\n");
        var parameters = new ReportParameters(DateTimeOffset.UnixEpoch, new Workspace("ws"), new RepoNameFilter(""), [],
            RepoSearchMode.SearchByFilter, PrTimeFilterMode.CreatedOnOnly, []);
        var accumulator = new ReportAccumulator(parameters, people);
        accumulator.RegisterDeveloper(new DeveloperIdentity(new UserUuid("a"), new DisplayName("Alex Kim")));
        accumulator.RegisterDeveloper(new DeveloperIdentity(new UserUuid("{A}"), new DisplayName("alex Kim")));
        accumulator.RegisterDeveloper(new DeveloperIdentity(new UserUuid("a"), new DisplayName("alex Kim")));
        accumulator.RegisterDeveloper(new DeveloperIdentity(new UserUuid("a"), new DisplayName("Alex Kimm")));
        var report = accumulator.Build();
        report.GetNameMismatches().Select(x => x.BitbucketName).Should().BeEquivalentTo("alex Kim", "Alex Kimm");
    }

    [Fact]
    public async Task UnmatchedUuidAndMissingUuidDoNotCreateNameMismatch()
    {
        var people = await ReadCsv("Name;Grade;Department;UUID\nAlex Kim;Senior;Platform;a\n");
        var data = CreateReport(people, "Platform");
        data.ObserveDeveloperIdentity(new DeveloperIdentity(null, new DisplayName("Wrong name")));
        data.ObserveDeveloperIdentity(new DeveloperIdentity(new UserUuid("unmatched"), new DisplayName("Wrong name")));
        data.GetNameMismatches().Should().BeEmpty();
        data.IsDeveloperIncluded(new DeveloperIdentity(new UserUuid("unmatched"), new DisplayName("Alex Kim"))).Should().BeFalse();
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
    [InlineData("Alex Kim;Senior;Import;\nAlex Kim;Senior;Export;UUID1\nAlex Kim;Senior;Export;UUID2\n", false)]
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
            (await read.Should().ThrowAsync<PeopleCsvFormatException>()).Which.Message.Should().Contain("line");
        }
    }

    [Fact]
    public async Task DuplicateUuidIsRejectedAcrossDifferentNamesAndNormalizedBraces()
    {
        Func<Task> read = async () => { _ = await ReadCsv("Name;Grade;Department;UUID\nAlice;Senior;Core;{AB}\nBob;Junior;Other;ab\n"); };
        (await read.Should().ThrowAsync<PeopleCsvFormatException>()).Which.Message.Should().Contain("lines 2 and 3");
    }

    [Theory]
    [InlineData("Name;Grade;Department\nAlex;Senior;Core\n")]
    [InlineData("Name;Grade;Department;UUID\nAlex;Senior;Core;\n")]
    [InlineData("Name;Grade;Department;UUID\nAlex;Senior;Core;{}\n")]
    [InlineData("Name;Grade;Department;UUID\nAlex;Senior;Core;   \n")]
    public async Task MissingUuidIsRejected(string csv)
    {
        Func<Task> read = async () => { _ = await ReadCsv(csv); };
        await read.Should().ThrowAsync<PeopleCsvFormatException>();
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

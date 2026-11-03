using FluentAssertions;

using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreRunSettingsPresenterTests
{
    [Fact(DisplayName = "Excluded dates combine weekends and configured days within the report window")]
    public void GetExcludedDatesInWindowCombinesRulesWithoutDuplicates()
    {
        var parameters = CreateParameters(
            new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
            excludeWeekend: true,
            excludedDays: [new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 11)]);

        var result = SpectreRunSettingsPresenter.GetExcludedDatesInWindow(
            parameters, new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));

        result.Should().Equal(
            new DateOnly(2026, 11, 7),
            new DateOnly(2026, 11, 8),
            new DateOnly(2026, 11, 9));
    }

    [Fact(DisplayName = "Forced included Saturday is shown as working in startup date summary")]
    public void GetExcludedDatesInWindowOmitsForcedIncludedSaturday()
    {
        var parameters = CreateParameters(
            new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
            excludeWeekend: true,
            excludedDays: [new DateOnly(2026, 11, 7)],
            forcedIncludeDates: [new DateOnly(2026, 11, 7)]);

        var excluded = SpectreRunSettingsPresenter.GetExcludedDatesInWindow(
            parameters, new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));
        var output = TestConsoleRunner.Run(_ => SpectreRunSettingsPresenter.Render(
            parameters, "report-user", htmlFileName: null, pdfFileName: null));

        excluded.Should().Equal(new DateOnly(2026, 11, 8));
        output.Should().Contain("Forced included dates");
        output.Should().Contain("2026-11-07");
    }

    [Fact(DisplayName = "Startup summary highlights detailed developer output")]
    public void RenderWhenDeveloperDetailsEnabledShowsWarning()
    {
        var parameters = CreateParameters(
            new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero),
            showAllDetailsForDevelopers: true);

        var output = TestConsoleRunner.Run(_ => SpectreRunSettingsPresenter.Render(
            parameters, "report-user", "core-bbdevpulse-report.html", "core-bbdevpulse-report.pdf"));

        output.Should().Contain("BBDevPulse  /  Review run");
        output.Should().Contain("01  Date range");
        output.Should().Contain("02  Scope");
        output.Should().Contain("Workspace");
        output.Should().Contain("workspace");
        output.Should().Contain("Bitbucket user");
        output.Should().Contain("report-user");
        output.Should().Contain("03  Report behavior");
        output.Should().Contain("04  Output files");
        output.Should().Contain("core-bbdevpulse-report.html");
        output.Should().Contain("core-bbdevpulse-report.pdf");
        output.Should().Contain("Developer details");
        output.Should().Contain("TIME WARNING");
    }

    [Fact(DisplayName = "Startup summary marks disabled output formats")]
    public void RenderWhenFormatsDisabledShowsDisabled()
    {
        var parameters = CreateParameters(
            new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero));

        var output = TestConsoleRunner.Run(_ => SpectreRunSettingsPresenter.Render(
            parameters, "report-user", htmlFileName: null, pdfFileName: null));

        output.Should().Contain("04  Output files");
        output.Should().Contain("HTML");
        output.Should().Contain("PDF");
        output.Should().Contain("Disabled");
    }

    private static ReportParameters CreateParameters(
        DateTimeOffset start,
        DateTimeOffset end,
        bool excludeWeekend = false,
        IReadOnlyList<DateOnly>? excludedDays = null,
        bool showAllDetailsForDevelopers = false,
        IReadOnlyList<DateOnly>? forcedIncludeDates = null) =>
        new(
            start,
            new Workspace("workspace"),
            new RepoNameFilter(""),
            [],
            RepoSearchMode.SearchByFilter,
            PrTimeFilterMode.CreatedOnOnly,
            [],
            excludeWeekend: excludeWeekend,
            excludedDays: excludedDays,
            showAllDetailsForDevelopers: showAllDetailsForDevelopers,
            toDateExclusive: end,
            forcedIncludeDates: forcedIncludeDates);
}

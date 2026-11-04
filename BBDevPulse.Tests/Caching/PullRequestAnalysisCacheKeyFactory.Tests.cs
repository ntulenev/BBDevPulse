using BBDevPulse.Caching;
using BBDevPulse.Models;

using FluentAssertions;

namespace BBDevPulse.Tests.Caching;

public sealed class PullRequestAnalysisCacheKeyFactoryTests
{
    [Fact]
    public void FingerprintChangesForCachedDataInputs()
    {
        var keys = new PullRequestAnalysisCacheKeyFactory();
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var baseline = Parameters(date, false);
        keys.BuildParametersFingerprint(baseline).Should().Be(keys.BuildParametersFingerprint(Parameters(date, false)));
        keys.BuildParametersFingerprint(baseline).Should().NotBe(keys.BuildParametersFingerprint(Parameters(date.AddDays(1), false)));
        keys.BuildParametersFingerprint(baseline).Should().NotBe(keys.BuildParametersFingerprint(Parameters(date, true)));

        var first = PullRequest(date, date.AddDays(1));
        var updated = PullRequest(date, date.AddDays(2));
        keys.BuildPullRequestFingerprint(first).Should().NotBe(keys.BuildPullRequestFingerprint(updated));
    }

    private static ReportParameters Parameters(DateTimeOffset date, bool details) =>
        new(date, new Workspace("ws"), new RepoNameFilter(""), [], RepoSearchMode.SearchByFilter,
            PrTimeFilterMode.CreatedOnOnly, [], showAllDetailsForDevelopers: details);

    private static PullRequest PullRequest(DateTimeOffset created, DateTimeOffset updated) =>
        new(new PullRequestId(1), PullRequestState.Open, null, created, updated, null,
            new User(new DisplayName("Alice"), new UserUuid("{a}")), null);
}

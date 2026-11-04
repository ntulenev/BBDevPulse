using BBDevPulse.Models;

namespace BBDevPulse.Logic;

/// <summary>
/// A fixed synthetic data set positioned within the latest 30-day window for visual previews.
/// </summary>
internal static class DemoReportData
{
    private static readonly string[] Developers = ["Avery Stone", "Jordan Lee", "Morgan Vale", "Taylor Quinn"];
    private static readonly string[] Repositories = ["Atlas API", "Northstar Web", "Orbit Worker"];
    private static readonly string[] RepositorySlugs = ["atlas-api", "northstar-web", "orbit-worker"];

    private static readonly DemoPullRequest[] PullRequests =
    [
        new(3, 0, 0, PullRequestState.Merged, 4, 2, 3, 86, 19, 28),
        new(5, 1, 1, PullRequestState.Merged, 6, 1, 7, 210, 48, 50),
        new(7, 2, 2, PullRequestState.Declined, 3, 2, 2, 43, 12, 32),
        new(9, 0, 3, PullRequestState.Merged, 8, 3, 12, 390, 144, 76),
        new(11, 1, 0, PullRequestState.Merged, 2, 0, 1, 24, 7, 19),
        new(13, 2, 1, PullRequestState.Open, 5, 2, 5, 156, 41, 30),
        new(15, 0, 2, PullRequestState.Merged, 7, 1, 9, 265, 83, 44),
        new(17, 1, 3, PullRequestState.Merged, 4, 2, 4, 112, 28, 26),
        new(19, 2, 0, PullRequestState.Superseded, 1, 1, 2, 39, 8, 22),
        new(21, 0, 1, PullRequestState.Merged, 9, 3, 15, 470, 126, 63),
        new(23, 1, 2, PullRequestState.Open, 3, 1, 6, 174, 52, 27),
        new(26, 2, 3, PullRequestState.Merged, 5, 2, 8, 220, 67, 38)
    ];

    public static ReportData Create(IReadOnlyDictionary<DisplayName, PersonCsvRow> peopleByName, string? teamFilter = null)
    {
        var start = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(-30);
        var end = start.AddDays(31);
        var parameters = new ReportParameters(
            start,
            new Workspace("demo-workspace"),
            new RepoNameFilter(string.Empty),
            [],
            RepoSearchMode.SearchByFilter,
            PrTimeFilterMode.LastKnownUpdateAndCreated,
            [new BranchName("main")],
            excludeWeekend: true,
            excludedDays: [DateOnly.FromDateTime(start.AddDays(11).Date)],
            pullRequestSizeMode: PullRequestSizeMode.Files,
            teamFilter: teamFilter,
            showDeveloperUuidInStats: false,
            showAllDetailsForDevelopers: true,
            toDateExclusive: end,
            isDemo: true);
        var reportData = new ReportData(parameters, peopleByName);
        var stats = Developers.Select(name =>
        {
            var identity = new DeveloperIdentity(new UserUuid("demo:" + name), new DisplayName(name));
            if (!reportData.IsDeveloperIncluded(identity))
            {
                return null;
            }

            var developer = reportData.GetOrAddDeveloper(identity);
            if (peopleByName.TryGetValue(identity.DisplayName, out var person))
            {
                developer.Grade = person.Grade;
                developer.Department = person.Department;
            }
            return developer;
        }).ToArray();

        for (var index = 0; index < PullRequests.Length; index++)
        {
            AddPullRequest(reportData, stats, PullRequests[index], index, start);
        }

        return reportData;
    }

    private static void AddPullRequest(
        ReportData reportData, DeveloperStats?[] stats, DemoPullRequest item, int index, DateTimeOffset start)
    {
        var authorStats = stats[item.Author];
        var reviewer = stats[(item.Author + 1) % stats.Length];
        if (authorStats is null && reviewer is null)
        {
            return;
        }

        var created = start.AddDays(item.Day).AddHours(9 + (index % 5));
        var closed = created.AddHours(item.DurationHours);
        var repo = Repositories[item.Repository];
        var slug = RepositorySlugs[item.Repository];
        var author = Developers[item.Author];
        var id = new PullRequestId(101 + index);
        var report = new PullRequestReport(
            repo, slug, author, "main", created, closed,
            item.State == PullRequestState.Merged ? closed : null,
            item.State is PullRequestState.Declined or PullRequestState.Superseded ? closed : null,
            item.State, id, item.Comments, item.Corrections,
            firstReactionOn: created.AddHours(2 + (index % 5)),
            filesChanged: item.Files,
            linesAdded: item.Added,
            linesRemoved: item.Removed,
            isActivityOnlyMatch: authorStats is null);
        reportData.Reports.Add(report);

        if (authorStats is not null)
        {
            authorStats.PrsOpenedSince++;
            if (item.State == PullRequestState.Merged)
            {
                authorStats.PrsMergedAfter++;
            }

            authorStats.Corrections += item.Corrections;
            authorStats.AuthoredPullRequests.Add(report);
            if (item.Corrections > 0)
            {
                authorStats.CommitActivities.Add(new DeveloperCommitActivity(
                    repo, slug, id,
                    (101 + index).ToString("x40", System.Globalization.CultureInfo.InvariantCulture),
                    "Refine synthetic implementation",
                    created.AddHours(8),
                    new PullRequestSizeSummary(item.Files, item.Added, item.Removed)));
            }
        }

        if (reviewer is not null)
        {
            reviewer.CommentsAfter += item.Comments;
            reviewer.PeerCommentsAfter += item.Comments;
            reviewer.ApprovalsAfter++;
            for (var comment = 0; comment < item.Comments; comment++)
            {
                reviewer.CommentActivities.Add(new DeveloperCommentActivity(
                    repo, slug, id, author, created.AddHours(3 + comment)));
            }

            reviewer.ApprovalActivities.Add(new DeveloperApprovalActivity(
                repo, slug, id, author, created.AddHours(5)));
        }
    }

    private readonly record struct DemoPullRequest(
        int Day,
        int Repository,
        int Author,
        PullRequestState State,
        int Comments,
        int Corrections,
        int Files,
        int Added,
        int Removed,
        int DurationHours);
}

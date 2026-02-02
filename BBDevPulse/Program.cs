using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;


var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var settings = LoadSettings(config);

var filterDate = DateTimeOffset.UtcNow.AddDays(-settings.Days);
var workspace = settings.Workspace;
var repoNameFilter = settings.RepoNameFilter;
var repoNameList = settings.RepoNameList;
var repoSearchMode = settings.RepoSearchMode;
var prTimeFilterMode = settings.PrTimeFilterMode;
var pageLength = settings.PageLength;
var username = settings.Username;
var appPassword = settings.AppPassword;

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
};

var authBytes = Encoding.ASCII.GetBytes($"{username}:{appPassword}");
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

var client = new BitbucketClient(httpClient);

var reports = new List<PullRequestReport>();
var developerStats = new Dictionary<string, DeveloperStats>(StringComparer.OrdinalIgnoreCase);

await AnnounceAuthAsync(client);

var filteredRepos = await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("green"))
    .StartAsync("Filtering repositories...", async _ =>
    {
        var repositories = await client.GetAllPagesAsync<Repository>(
            $"repositories/{workspace}?pagelen={pageLength}",
            page => AnsiConsole.MarkupLine($"[grey]Filtering repositories: page {page}[/]"));
        return repositories
            .Where(repo => RepoMatchesFilter(repo, repoSearchMode, repoNameFilter, repoNameList))
            .ToList();
    });

RenderRepositoryTable(filteredRepos, repoSearchMode, repoNameFilter, repoNameList);

await AnsiConsole.Progress()
    .AutoClear(false)
    .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new SpinnerColumn(),
        new ElapsedTimeColumn(),
        new RemainingTimeColumn(),
        new TaskDescriptionColumn())
    .StartAsync(async context =>
    {
        var task = context.AddTask("Analyzing repositories", maxValue: filteredRepos.Count);

        foreach (var repo in filteredRepos)
        {
            if (string.IsNullOrWhiteSpace(repo.Slug))
            {
                task.Increment(1);
                continue;
            }

            task.Description = $"Analyzing {repo.Name ?? repo.Slug}";

            var prUrl =
                $"repositories/{workspace}/{repo.Slug}/pullrequests?pagelen={pageLength}&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on";

            var nextPrPage = prUrl;
            var shouldStop = false;

            while (!string.IsNullOrWhiteSpace(nextPrPage) && !shouldStop)
            {
                var page = await client.GetPageAsync<PullRequest>(nextPrPage);
                var pullRequests = page.Values ?? [];

                foreach (var pr in pullRequests)
                {
                    if (ShouldStopByTimeFilter(pr, filterDate, prTimeFilterMode))
                    {
                        shouldStop = true;
                        break;
                    }

                    var lastActivity = pr.CreatedOn;
                    var authorIdentity = BuildDeveloperKey(pr.Author);
                    var shouldCalculateTtfr = pr.CreatedOn >= filterDate;
                    DateTimeOffset? firstReactionOn = null;
                    DateTimeOffset? mergedOnFromActivity = null;
                    var participants = new Dictionary<string, DeveloperIdentity>(StringComparer.OrdinalIgnoreCase);
                    var commentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var totalComments = 0;
                    var approvalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    var activityUrl =
                        $"repositories/{workspace}/{repo.Slug}/pullrequests/{pr.Id}/activity?pagelen={pageLength}&sort=-created_on";
                    var nextActivityPage = activityUrl;
                    var stopActivities = false;

                    while (!string.IsNullOrWhiteSpace(nextActivityPage) && !stopActivities)
                    {
                        var activityPage = await client.GetPageAsync<JsonElement>(nextActivityPage);
                        var activities = activityPage.Values ?? [];

                        foreach (var activity in activities)
                        {
                            var activityDate = TryGetActivityDate(activity);
                            if (activityDate.HasValue && activityDate.Value < filterDate)
                            {
                                stopActivities = true;
                                break;
                            }

                            if (activityDate.HasValue && activityDate.Value > lastActivity)
                            {
                                lastActivity = activityDate.Value;
                            }

                            if (TryGetMergeDate(activity, out var mergeDate))
                            {
                                if (!mergedOnFromActivity.HasValue || mergeDate < mergedOnFromActivity.Value)
                                {
                                    mergedOnFromActivity = mergeDate;
                                }
                            }

                            var activityUser = TryGetActivityUser(activity);
                            if (activityUser.HasValue)
                            {
                                AddParticipant(participants, activityUser.Value);

                                if (TryGetCommentInfo(activity, out var commentUser, out var commentDate))
                                {
                                    totalComments++;
                                    if (commentDate >= filterDate)
                                    {
                                        var commentKey = commentUser.Uuid ?? commentUser.DisplayName;
                                        commentCounts[commentKey] = commentCounts.GetValueOrDefault(commentKey) + 1;
                                        AddParticipant(participants, commentUser);
                                    }

                                    if (shouldCalculateTtfr &&
                                        commentDate >= pr.CreatedOn &&
                                        (!authorIdentity.HasValue ||
                                         !IsSameIdentity(authorIdentity.Value, commentUser)))
                                    {
                                        if (!firstReactionOn.HasValue || commentDate < firstReactionOn.Value)
                                        {
                                            firstReactionOn = commentDate;
                                        }
                                    }
                                }

                                if (TryGetApprovalInfo(activity, out var approvalUser, out var approvalDate))
                                {
                                    if (approvalDate >= filterDate)
                                    {
                                        var approvalKey = approvalUser.Uuid ?? approvalUser.DisplayName;
                                        approvalCounts[approvalKey] = approvalCounts.GetValueOrDefault(approvalKey) + 1;
                                        AddParticipant(participants, approvalUser);
                                    }

                                    if (shouldCalculateTtfr &&
                                        approvalDate >= pr.CreatedOn &&
                                        (!authorIdentity.HasValue ||
                                         !IsSameIdentity(authorIdentity.Value, approvalUser)))
                                    {
                                        if (!firstReactionOn.HasValue || approvalDate < firstReactionOn.Value)
                                        {
                                            firstReactionOn = approvalDate;
                                        }
                                    }
                                }
                            }
                        }

                        nextActivityPage = activityPage.Next;
                    }

                var matchesFilter = pr.CreatedOn >= filterDate || lastActivity >= filterDate;
                if (!matchesFilter)
                {
                    continue;
                }

                if (authorIdentity.HasValue)
                {
                    participants[authorIdentity.Value.Uuid ?? authorIdentity.Value.DisplayName] =
                        authorIdentity.Value;
                }

                var mergedOnResolved = mergedOnFromActivity ?? pr.MergedOn;
                if (authorIdentity.HasValue)
                {
                    if (pr.CreatedOn >= filterDate)
                    {
                        GetOrAddDeveloper(developerStats, authorIdentity.Value).PrsOpenedSince++;
                    }

                    if (mergedOnResolved.HasValue && mergedOnResolved.Value >= filterDate)
                    {
                        GetOrAddDeveloper(developerStats, authorIdentity.Value).PrsMergedAfter++;
                    }
                }

                foreach (var entry in commentCounts)
                {
                    if (participants.TryGetValue(entry.Key, out var participant))
                    {
                        GetOrAddDeveloper(developerStats, participant).CommentsAfter += entry.Value;
                    }
                }

                foreach (var entry in approvalCounts)
                {
                    if (participants.TryGetValue(entry.Key, out var participant))
                    {
                        GetOrAddDeveloper(developerStats, participant).ApprovalsAfter += entry.Value;
                    }
                }

                    reports.Add(new PullRequestReport(
                        repo.Name ?? repo.Slug ?? "unknown",
                        pr.Author?.DisplayName ?? "unknown",
                        pr.CreatedOn,
                        lastActivity,
                        mergedOnResolved,
                        ResolveRejectedOn(pr),
                        pr.State,
                        pr.Id,
                        totalComments,
                        firstReactionOn
                    ));
                }

                nextPrPage = page.Next;
            }

            task.Increment(1);
        }
    });

var sortedReports = reports
    .OrderBy(r => r.CreatedOn)
    .ToList();

RenderPullRequestTable(sortedReports);
RenderMergeTimeStats(sortedReports);
RenderTtfrStats(sortedReports);
RenderDeveloperStatsTable(developerStats, filterDate);

static DateTimeOffset PromptForDate(string label)
{
    var text = AnsiConsole.Prompt(
        new TextPrompt<string>(label)
            .PromptStyle("green")
            .DefaultValue(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"))
            .Validate(input => DateTimeOffset.TryParseExact(
                    input,
                    "yyyy-MM-dd",
                    null,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out _)
                ? ValidationResult.Success()
                : ValidationResult.Error("[red]Use yyyy-MM-dd.[/]")));

    return DateTimeOffset.ParseExact(
        text,
        "yyyy-MM-dd",
        null,
        System.Globalization.DateTimeStyles.AssumeUniversal);
}

static async Task AnnounceAuthAsync(BitbucketClient client)
{
    AnsiConsole.MarkupLine("[grey]Authenticating with Bitbucket...[/]");
    try
    {
        var user = await client.GetSingleAsync<AuthUser>("user");
        var name = user.DisplayName ?? user.Username ?? user.Uuid ?? "unknown";
        AnsiConsole.MarkupLine($"[green]Auth succeeded for user:[/] {name}");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Auth failed:[/] {ex.Message}");
        throw;
    }
}

static string PromptForText(string label)
{
    return AnsiConsole.Prompt(
        new TextPrompt<string>(label)
            .PromptStyle("green")
            .Validate(text => string.IsNullOrWhiteSpace(text)
                ? ValidationResult.Error("[red]Value is required.[/]")
                : ValidationResult.Success()));
}

static string PromptForTextAllowEmpty(string label)
{
    return AnsiConsole.Prompt(
        new TextPrompt<string>(label)
            .PromptStyle("green")
            .AllowEmpty()
            .DefaultValue(string.Empty));
}

static int PromptForInt(string label, int defaultValue, int min, int max)
{
    return AnsiConsole.Prompt(
        new TextPrompt<int>(label)
            .PromptStyle("green")
            .DefaultValue(defaultValue)
            .Validate(value => value >= min && value <= max
                ? ValidationResult.Success()
                : ValidationResult.Error($"[red]Enter a value between {min} and {max}.[/]")));
}

static string GetEnvOrPrompt(string envName, string label)
{
    var value = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    return PromptForText(label);
}

static string GetEnvOrPromptSecret(string envName, string label)
{
    var value = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    return AnsiConsole.Prompt(
        new TextPrompt<string>(label)
            .PromptStyle("green")
            .Secret()
            .Validate(text => string.IsNullOrWhiteSpace(text)
                ? ValidationResult.Error("[red]Value is required.[/]")
                : ValidationResult.Success()));
}

static AppSettings LoadSettings(IConfiguration config)
{
    var section = config.GetSection("Bitbucket");
    if (!section.Exists())
    {
        throw new InvalidOperationException("Missing Bitbucket section in appsettings.json.");
    }

    var repoList = section.GetSection("RepoNameList").Get<string[]>() ?? Array.Empty<string>();
    var searchMode = GetRequiredEnum<RepoSearchMode>(section, "RepoSearchMode");
    var prTimeFilterMode = GetOptionalEnum(
        section,
        "PrTimeFilterMode",
        PrTimeFilterMode.LastKnownUpdateAndCreated);

    return new AppSettings(
        GetRequiredInt(section, "Days"),
        GetRequiredString(section, "Workspace"),
        GetRequiredInt(section, "PageLength"),
        GetRequiredString(section, "Username"),
        GetRequiredString(section, "AppPassword"),
        GetOptionalString(section, "RepoNameFilter"),
        repoList,
        searchMode,
        prTimeFilterMode);
}

static string GetRequiredString(IConfiguration section, string key)
{
    var value = section[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing Bitbucket:{key} in appsettings.json.");
    }

    return value;
}

static string GetOptionalString(IConfiguration section, string key)
{
    return section[key] ?? string.Empty;
}

static int GetRequiredInt(IConfiguration section, string key)
{
    var value = section[key];
    if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out var parsed))
    {
        throw new InvalidOperationException($"Missing or invalid Bitbucket:{key} in appsettings.json.");
    }

    return parsed;
}

static T GetRequiredEnum<T>(IConfiguration section, string key) where T : struct
{
    var value = section[key];
    if (string.IsNullOrWhiteSpace(value) || !Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
    {
        throw new InvalidOperationException($"Missing or invalid Bitbucket:{key} in appsettings.json.");
    }

    return parsed;
}

static T GetOptionalEnum<T>(IConfiguration section, string key, T defaultValue) where T : struct
{
    var value = section[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    if (!Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
    {
        throw new InvalidOperationException($"Invalid Bitbucket:{key} in appsettings.json.");
    }

    return parsed;
}

static bool ShouldStopByTimeFilter(
    PullRequest pr,
    DateTimeOffset filterDate,
    PrTimeFilterMode filterMode)
{
    return filterMode switch
    {
        PrTimeFilterMode.CreatedOnOnly => pr.CreatedOn < filterDate,
        _ =>
            (pr.UpdatedOn ?? pr.CreatedOn) < filterDate &&
            pr.CreatedOn < filterDate
    };
}

static void RenderPullRequestTable(IReadOnlyCollection<PullRequestReport> reports)
{
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("#")
        .AddColumn("Repository")
        .AddColumn("Author")
        .AddColumn("Created")
        .AddColumn("TTFR")
        .AddColumn("Last Activity")
        .AddColumn("Merged")
        .AddColumn("Rejected")
        .AddColumn("PR Age (days)")
        .AddColumn("Time to Merge")
        .AddColumn("Comments")
        .AddColumn("PR ID");

    var index = 1;
    foreach (var report in reports)
    {
        var timeToMerge = report.MergedOn.HasValue
            ? FormatDuration((report.MergedOn.Value - report.CreatedOn).TotalDays)
            : "-";
        var prAge = string.Equals(report.State, "OPEN", StringComparison.OrdinalIgnoreCase)
            ? (DateTimeOffset.UtcNow - report.CreatedOn).TotalDays.ToString("0.0")
            : "-";
        var ttfr = report.FirstReactionOn.HasValue
            ? FormatDuration((report.FirstReactionOn.Value - report.CreatedOn).TotalDays)
            : "-";
        table.AddRow(
            index.ToString(),
            report.Repository,
            report.Author,
            report.CreatedOn.ToString("yyyy-MM-dd"),
            ttfr,
            report.LastActivity.ToString("yyyy-MM-dd"),
            report.MergedOn?.ToString("yyyy-MM-dd") ?? "-",
            report.RejectedOn?.ToString("yyyy-MM-dd") ?? "-",
            prAge,
            timeToMerge,
            report.Comments.ToString(),
            report.Id.ToString()
        );

        index++;
    }

    AnsiConsole.Write(new Rule("Pull Requests").RuleStyle("grey"));
    AnsiConsole.Write(table);
}

static void RenderRepositoryTable(
    IReadOnlyCollection<Repository> repositories,
    RepoSearchMode searchMode,
    string filter,
    IReadOnlyList<string> repoList)
{
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("#")
        .AddColumn("Repository")
        .AddColumn("Slug");

    var index = 1;
    foreach (var repo in repositories.OrderBy(r => r.Name ?? r.Slug ?? string.Empty))
    {
        table.AddRow(index.ToString(), repo.Name ?? "-", repo.Slug ?? "-");
        index++;
    }

    var title = searchMode switch
    {
        RepoSearchMode.FilterFromTheList when repoList.Count > 0
            => $"Repositories (list count: {repoList.Count})",
        RepoSearchMode.SearchByFilter when !string.IsNullOrWhiteSpace(filter)
            => $"Repositories (contains: {filter})",
        _ => "Repositories (all)"
    };

    AnsiConsole.Write(new Rule(title).RuleStyle("grey"));
    AnsiConsole.Write(table);
}

static void RenderMergeTimeStats(IReadOnlyCollection<PullRequestReport> reports)
{
    var mergeDays = reports
        .Where(r => r.MergedOn.HasValue)
        .Select(r => (r.MergedOn.Value - r.CreatedOn).TotalDays)
        .OrderBy(days => days)
        .ToList();

    if (mergeDays.Count == 0)
    {
        AnsiConsole.Write(new Rule("Merge Time Stats").RuleStyle("grey"));
        AnsiConsole.MarkupLine("[yellow]No merged pull requests in the report.[/]");
        return;
    }

    var best = mergeDays.First();
    var longest = mergeDays.Last();
    var median = Percentile(mergeDays, 50);
    var p75 = Percentile(mergeDays, 75);

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Metric")
        .AddColumn("Time");

    table.AddRow("Best Merge Time", FormatDuration(best));
    table.AddRow("Longest Merge Time", FormatDuration(longest));
    table.AddRow("Median", FormatDuration(median));
    table.AddRow("75P", FormatDuration(p75));

    AnsiConsole.Write(new Rule("Merge Time Stats").RuleStyle("grey"));
    AnsiConsole.Write(table);
}

static void RenderTtfrStats(IReadOnlyCollection<PullRequestReport> reports)
{
    var ttfrDays = reports
        .Where(r => r.FirstReactionOn.HasValue)
        .Select(r => (r.FirstReactionOn!.Value - r.CreatedOn).TotalDays)
        .OrderBy(days => days)
        .ToList();

    if (ttfrDays.Count == 0)
    {
        AnsiConsole.Write(new Rule("TTFR Stats").RuleStyle("grey"));
        AnsiConsole.MarkupLine("[yellow]No TTFR data available in the report.[/]");
        return;
    }

    var best = ttfrDays.First();
    var longest = ttfrDays.Last();
    var median = Percentile(ttfrDays, 50);
    var p75 = Percentile(ttfrDays, 75);

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Metric")
        .AddColumn("Time");

    table.AddRow("Best TTFR", FormatDuration(best));
    table.AddRow("Longest TTFR", FormatDuration(longest));
    table.AddRow("Median", FormatDuration(median));
    table.AddRow("75P", FormatDuration(p75));

    AnsiConsole.Write(new Rule("TTFR Stats").RuleStyle("grey"));
    AnsiConsole.Write(table);
}

static double Percentile(IReadOnlyList<double> sortedValues, int percentile)
{
    if (sortedValues.Count == 0)
    {
        return 0;
    }

    var position = (percentile / 100.0) * (sortedValues.Count - 1);
    var lowerIndex = (int)Math.Floor(position);
    var upperIndex = (int)Math.Ceiling(position);
    if (lowerIndex == upperIndex)
    {
        return sortedValues[lowerIndex];
    }

    var weight = position - lowerIndex;
    return sortedValues[lowerIndex] + weight * (sortedValues[upperIndex] - sortedValues[lowerIndex]);
}

static string FormatDuration(double days)
{
    if (days < 1)
    {
        var hours = days * 24;
        if (hours < 1)
        {
            return "<1h";
        }

        return $"{hours:0.0} hours";
    }

    return $"{days:0.0} days";
}

static void RenderDeveloperStatsTable(
    IReadOnlyDictionary<string, DeveloperStats> stats,
    DateTimeOffset filterDate)
{
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("#")
        .AddColumn("Developer")
        .AddColumn("PRs Opened")
        .AddColumn("PRs Merged")
        .AddColumn("Comments")
        .AddColumn("Approvals");

    var index = 1;
    foreach (var stat in stats.Values
        .OrderByDescending(s => s.PrsOpenedSince)
        .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase))
    {
        table.AddRow(
            index.ToString(),
            stat.DisplayName,
            stat.PrsOpenedSince.ToString(),
            stat.PrsMergedAfter.ToString(),
            stat.CommentsAfter.ToString(),
            stat.ApprovalsAfter.ToString()
        );
        index++;
    }

    AnsiConsole.Write(new Rule($"Developer Stats (since {filterDate:yyyy-MM-dd})").RuleStyle("grey"));
    AnsiConsole.Write(table);
}

static DateTimeOffset? TryGetActivityDate(JsonElement activity)
{
    if (TryGetDate(activity, out var date, "comment", "created_on")) return date;
    if (TryGetDate(activity, out date, "approval", "date")) return date;
    if (TryGetDate(activity, out date, "approval", "approved_on")) return date;
    if (TryGetDate(activity, out date, "update", "date")) return date;
    if (TryGetDate(activity, out date, "pullrequest", "updated_on")) return date;
    if (TryGetDate(activity, out date, "pullrequest", "created_on")) return date;
    if (TryGetDate(activity, out date, "created_on")) return date;
    if (TryGetDate(activity, out date, "date")) return date;
    if (TryGetDate(activity, out date, "updated_on")) return date;
    return null;
}

static bool TryGetMergeDate(JsonElement activity, out DateTimeOffset date)
{
    if (TryGetString(activity, out var state, "pullrequest", "state") &&
        string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase))
    {
        if (TryGetDate(activity, out date, "pullrequest", "merged_on")) return true;
        if (TryGetDate(activity, out date, "pullrequest", "updated_on")) return true;
        if (TryGetDate(activity, out date, "date")) return true;
        if (TryGetDate(activity, out date, "created_on")) return true;
    }

    if (TryGetString(activity, out state, "update", "state") &&
        string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase))
    {
        if (TryGetDate(activity, out date, "update", "date")) return true;
        if (TryGetDate(activity, out date, "date")) return true;
    }

    if (TryGetDate(activity, out date, "merge", "date")) return true;
    if (TryGetDate(activity, out date, "merge", "created_on")) return true;

    date = default;
    return false;
}

static bool TryGetCommentInfo(JsonElement activity, out DeveloperIdentity user, out DateTimeOffset date)
{
    if (TryGetCommentInfoForPath(activity, out user, out date, "comment")) return true;
    if (TryGetCommentInfoForPath(activity, out user, out date, "pullrequest_comment")) return true;
    if (TryGetCommentInfoForPath(activity, out user, out date, "pull_request_comment")) return true;

    user = default;
    date = default;
    return false;
}

static bool TryGetCommentInfoForPath(
    JsonElement activity,
    out DeveloperIdentity user,
    out DateTimeOffset date,
    params string[] path)
{
    if (TryGetUser(activity, out user, path.Concat(new[] { "user" }).ToArray()))
    {
        if (TryGetDate(activity, out date, path.Concat(new[] { "created_on" }).ToArray()) ||
            TryGetDate(activity, out date, path.Concat(new[] { "updated_on" }).ToArray()) ||
            TryGetDate(activity, out date, "date") ||
            TryGetDate(activity, out date, "created_on"))
        {
            return true;
        }
    }

    user = default;
    date = default;
    return false;
}

static bool TryGetApprovalInfo(JsonElement activity, out DeveloperIdentity user, out DateTimeOffset date)
{
    if (TryGetUser(activity, out user, "approval", "user") &&
        (TryGetDate(activity, out date, "approval", "date") ||
         TryGetDate(activity, out date, "approval", "approved_on") ||
         TryGetDate(activity, out date, "date")))
    {
        return true;
    }

    user = default;
    date = default;
    return false;
}

static DeveloperIdentity? TryGetActivityUser(JsonElement activity)
{
    if (TryGetUser(activity, out var user, "comment", "user")) return user;
    if (TryGetUser(activity, out user, "approval", "user")) return user;
    if (TryGetUser(activity, out user, "update", "author")) return user;
    if (TryGetUser(activity, out user, "update", "user")) return user;
    if (TryGetUser(activity, out user, "pullrequest", "author")) return user;
    if (TryGetUser(activity, out user, "actor")) return user;
    if (TryGetUser(activity, out user, "user")) return user;
    return null;
}

static bool TryGetString(JsonElement element, out string value, params string[] path)
{
    if (TryGetNestedProperty(element, out var stringElement, path) &&
        stringElement.ValueKind == JsonValueKind.String)
    {
        value = stringElement.GetString() ?? string.Empty;
        return true;
    }

    value = string.Empty;
    return false;
}

static bool TryGetUser(JsonElement element, out DeveloperIdentity user, params string[] path)
{
    if (TryGetNestedProperty(element, out var userElement, path))
    {
        var displayName = GetString(userElement, "display_name") ??
            GetString(userElement, "nickname") ??
            GetString(userElement, "username");
        var uuid = GetString(userElement, "uuid");

        if (!string.IsNullOrWhiteSpace(displayName) || !string.IsNullOrWhiteSpace(uuid))
        {
            user = new DeveloperIdentity(uuid, displayName ?? uuid ?? "unknown");
            return true;
        }
    }

    user = default;
    return false;
}

static string? GetString(JsonElement element, string propertyName)
{
    if (element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var prop) &&
        prop.ValueKind == JsonValueKind.String)
    {
        return prop.GetString();
    }

    return null;
}

static bool TryGetDate(JsonElement element, out DateTimeOffset date, params string[] path)
{
    if (TryGetNestedProperty(element, out var dateElement, path) &&
        dateElement.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(dateElement.GetString(), out date))
    {
        return true;
    }

    date = default;
    return false;
}

static bool TryGetNestedProperty(JsonElement element, out JsonElement result, params string[] path)
{
    result = element;
    foreach (var segment in path)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty(segment, out var next))
        {
            return false;
        }

        result = next;
    }

    return true;
}

static DeveloperIdentity? BuildDeveloperKey(User? user)
{
    if (user is null)
    {
        return null;
    }

    return new DeveloperIdentity(user.Uuid, user.DisplayName ?? user.Uuid ?? "unknown");
}

static DeveloperStats GetOrAddDeveloper(
    IDictionary<string, DeveloperStats> stats,
    DeveloperIdentity identity)
{
    var key = identity.Uuid ?? identity.DisplayName;
    if (stats.TryGetValue(key, out var existing))
    {
        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            existing.DisplayName = identity.DisplayName;
        }

        return existing;
    }

    var created = new DeveloperStats(identity.DisplayName ?? identity.Uuid ?? "unknown");
    stats[key] = created;
    return created;
}

static void AddParticipant(
    IDictionary<string, DeveloperIdentity> participants,
    DeveloperIdentity identity)
{
    var key = identity.Uuid ?? identity.DisplayName;
    if (!participants.ContainsKey(key))
    {
        participants[key] = identity;
    }
}

static bool IsSameIdentity(DeveloperIdentity left, DeveloperIdentity right)
{
    if (!string.IsNullOrWhiteSpace(left.Uuid) && !string.IsNullOrWhiteSpace(right.Uuid))
    {
        return string.Equals(left.Uuid, right.Uuid, StringComparison.OrdinalIgnoreCase);
    }

    return string.Equals(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
}

static DateTimeOffset? ResolveRejectedOn(PullRequest pr)
{
    if (!string.Equals(pr.State, "DECLINED", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(pr.State, "SUPERSEDED", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    return pr.ClosedOn ?? pr.UpdatedOn;
}

static bool RepoMatchesFilter(
    Repository repo,
    RepoSearchMode searchMode,
    string filter,
    IReadOnlyList<string> repoList)
{
    var name = repo.Name ?? string.Empty;
    var slug = repo.Slug ?? string.Empty;

    return searchMode switch
    {
        RepoSearchMode.FilterFromTheList => repoList.Count == 0 ||
            repoList.Any(entry =>
                name.Equals(entry, StringComparison.OrdinalIgnoreCase) ||
                slug.Equals(entry, StringComparison.OrdinalIgnoreCase)),
        _ => string.IsNullOrWhiteSpace(filter) ||
            name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            slug.Contains(filter, StringComparison.OrdinalIgnoreCase)
    };
}

sealed class BitbucketClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public BitbucketClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<T>> GetAllPagesAsync<T>(string url, Action<int>? onPage = null)
    {
        var results = new List<T>();
        var next = url;
        var pageIndex = 0;

        while (!string.IsNullOrWhiteSpace(next))
        {
            pageIndex++;
            onPage?.Invoke(pageIndex);
            var page = await GetAsync<PaginatedResponse<T>>(next);
            if (page.Values is { Count: > 0 })
            {
                results.AddRange(page.Values);
            }

            next = page.Next;
        }

        return results;
    }

    public async Task<PaginatedResponse<T>> GetPageAsync<T>(string url)
    {
        return await GetAsync<PaginatedResponse<T>>(url);
    }

    public async Task<T> GetSingleAsync<T>(string url)
    {
        return await GetAsync<T>(url);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Bitbucket API request failed ({response.StatusCode}): {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        if (result == null)
        {
            throw new InvalidOperationException("Bitbucket API response was empty.");
        }

        return result;
    }
}

sealed class Repository
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }
}

sealed class AuthUser
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }
}

sealed class PaginatedResponse<T>
{
    [JsonPropertyName("values")]
    public List<T> Values { get; init; } = [];

    [JsonPropertyName("next")]
    public string? Next { get; init; }
}

sealed class PullRequest
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("closed_on")]
    public DateTimeOffset? ClosedOn { get; init; }

    [JsonPropertyName("created_on")]
    public DateTimeOffset CreatedOn { get; init; }

    [JsonPropertyName("updated_on")]
    public DateTimeOffset? UpdatedOn { get; init; }

    [JsonPropertyName("merged_on")]
    public DateTimeOffset? MergedOn { get; init; }

    [JsonPropertyName("author")]
    public User? Author { get; init; }
}

sealed class User
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }
}

sealed record PullRequestReport(
    string Repository,
    string Author,
    DateTimeOffset CreatedOn,
    DateTimeOffset LastActivity,
    DateTimeOffset? MergedOn,
    DateTimeOffset? RejectedOn,
    string? State,
    int Id,
    int Comments,
    DateTimeOffset? FirstReactionOn);

sealed class DeveloperStats
{
    public DeveloperStats(string displayName)
    {
        DisplayName = displayName;
    }

    public string DisplayName { get; set; }
    public int PrsOpenedSince { get; set; }
    public int PrsMergedAfter { get; set; }
    public int CommentsAfter { get; set; }
    public int ApprovalsAfter { get; set; }
}

readonly record struct DeveloperIdentity(string? Uuid, string DisplayName);

sealed record AppSettings(
    int Days,
    string Workspace,
    int PageLength,
    string Username,
    string AppPassword,
    string RepoNameFilter,
    IReadOnlyList<string> RepoNameList,
    RepoSearchMode RepoSearchMode,
    PrTimeFilterMode PrTimeFilterMode);

enum RepoSearchMode
{
    SearchByFilter = 1,
    FilterFromTheList = 2
}

enum PrTimeFilterMode
{
    LastKnownUpdateAndCreated = 1,
    CreatedOnOnly = 2
}

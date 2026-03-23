# BBDevPulse
<img src="BBDevPulse.png" alt="BBDevPulse logo" width="250">

BBDevPulse is a console analytics utility for tracking Bitbucket pull request activity.
It helps you see PR throughput, review load, merge speed, and per-developer contribution.

## How the service works
1. Loads configuration from `appsettings.json` (`Bitbucket` section).
2. Authenticates against Bitbucket Cloud API using basic auth (`Username` + `AppPassword`).
3. Fetches repositories from the configured workspace with pagination (`PageLength`).
4. Filters repositories using `RepoSearchMode`:
   - `SearchByFilter` -> uses `RepoNameFilter` substring match (name or slug).
   - `FilterFromTheList` -> uses exact matches from `RepoNameList` (name or slug).
5. Fetches pull requests for each selected repo (open, merged, declined, superseded), sorted by latest updates.
6. Applies PR time-stop mode (`PrTimeFilterMode`) to stop reading older PR pages earlier.
7. For each matching pull request, first checks the local file cache for previously downloaded analysis data.
8. On cache miss, fetches PR activity (comments/approvals/updates), correction commits, and PR diffstat size (added/removed lines), then saves the snapshot to the local cache.
9. On cache hit, reuses the saved snapshot and skips repeated Bitbucket requests for already analyzed PRs.
10. Applies the configured report window:
   - `Days` for a rolling lookback from now, or
   - `FromDate` + `ToDate` for an explicit inclusive date range.
11. Renders output tables:
   - Repositories included in analysis
   - Pull request report (includes `Size` T-shirt metric)
   - Merge-time statistics (best/median/75p/longest)
   - PR size statistics (smallest/biggest/median/75p by selected size mode)
   - Comments statistics (based on PR-level `Comments` totals)
   - Peer comments statistics (based on per-developer comments in other people's PRs)
   - Developer statistics (grade, department, PRs opened, merged, comments, peer comments, approvals, corrections)
   - Optional developer detail sections when `ShowAllDetailsForDevelopers = true`:
     - Authored PRs
     - Comments
     - Approvals
     - Follow-up commits
   - Worst PRs by metric (longest merge, longest TTFR, most corrections, biggest PR)
   - Bitbucket telemetry summary:
     - Real HTTP request count
     - Analysis cache hits/misses/stores
     - Estimated avoided requests
     - Estimated cache efficiency
     - Request breakdown by normalized Bitbucket API endpoint
12. Optionally generates an HTML report (`Bitbucket:Html` settings) and a PDF report using QuestPDF (`Bitbucket:Pdf` settings).
13. HTML report mirrors the PDF structure:
   - Detailed tables support sorting and filtering (`Pull Requests`, `Worst PRs by Metric`, `Developer Stats`, developer detail tables).
   - Summary stats tables (`Merge Time Stats`, `TTFR Stats`, `Corrections Stats`, `PR Size Stats`) are rendered as static read-only tables.

When `TeamFilter` is configured:
- All matching repositories and pull requests are still analyzed for activity.
- PR rows include pull requests whose author belongs to the selected team, plus PRs authored outside the team when the selected team had activity on them.
- External-author PRs shown because of team activity are highlighted in orange in console/PDF output.
- PR-based metrics still include only pull requests whose author belongs to the selected team.
- Developer output includes only developers from the selected team.
- Team members still get comment/approval activity counted even on PRs authored by people outside the team.

## Local cache and telemetry
BBDevPulse stores pull request analysis snapshots on disk and reuses them on the next run if the PR fingerprint has not changed.
This reduces repeated Bitbucket API calls for already analyzed pull requests.

- Cache location: `AppContext.BaseDirectory/cache/pull-request-analysis`
- Cache contents: PR activity, correction commits, PR size summary, and detailed follow-up commit activity
- Cache cleanup: no automatic eviction is performed; remove old cache files manually when needed
- Telemetry output: shown in console summary when `Bitbucket:Telemetry:Enabled = true`

## Comments and Peer Comments
BBDevPulse exposes two different comment-related metrics:

- `Comments`: total PR comment count on a pull request row. This is a PR-level metric and reflects all comments counted for that PR.
- `Peer Comments`: a developer-level metric that counts only comments made in pull requests authored by other developers.

How `Peer Comments` is calculated:

- The report iterates over comment activities that fall inside the selected date window.
- A comment is added to the comment author's `Comments` counter.
- The same comment is added to the author's `Peer Comments` counter only when the PR author is known and the comment author is not the same person as the PR author.
- Self-comments on your own pull requests are excluded from `Peer Comments`.
- If the PR author is unknown, the comment is not counted as a peer comment because the report cannot reliably decide whether the PR is foreign.

How `Peer Comments Stats` is calculated:

- Take `Peer Comments` totals for all developers included in the report, including zero values.
- Sort them ascending.
- `Min Peer Comments` = minimum developer value.
- `Max Peer Comments` = maximum developer value.
- `Median` = 50th percentile across developers.
- `75P` = 75th percentile across developers.

This makes `Peer Comments` a more honest signal of review participation than raw PR comment totals, because it excludes comments people leave on their own pull requests.

## PR Size calculation
PR size is based on Bitbucket `diffstat` between pull request source and destination commits.
The metric used for T-shirt sizing is controlled by `PullRequestSizeMode`.

For each PR:
- Resolve `source` and `destination` commit hashes from PR details.
- Request diffstat for commit range:
  - `repositories/{workspace}/{repo}/diffstat/{workspace}/{repo}:{sourceHash}..{destinationHash}?topic=true`
- Aggregate values:
  - `linesAdded` = sum of `lines_added`
  - `linesRemoved` = sum of `lines_removed`
  - `lineChurn` = `linesAdded + linesRemoved`

### `PullRequestSizeMode = Lines` (default)
T-shirt size (`Size`) is derived from `lineChurn`:
- `XS` -> `<= 100`
- `S` -> `101..300`
- `M` -> `301..700`
- `L` -> `701..1200`
- `XL` -> `> 1200`

### `PullRequestSizeMode = Files`
T-shirt size (`Size`) is derived from `filesChanged`:
- `XS` -> `<= 2`
- `S` -> `3..5`
- `M` -> `6..10`
- `L` -> `11..20`
- `XL` -> `> 20`

Where size is shown:
- Pull Requests table: `Size` column in format `Tier (value)` where value is `lineChurn` (Lines mode) or `filesChanged` (Files mode).
- PR Size Stats section:
  - `Smallest PR` = minimum value for selected mode
  - `Biggest PR` = maximum value for selected mode
  - `Median` = 50th percentile value for selected mode
  - `75P` = 75th percentile value for selected mode
- Worst PRs by Metric: `Biggest PR` is the PR with highest value for selected mode (distinct selection from other worst metrics).

If diffstat cannot be read for a PR (API error, missing commit hashes), size is treated as unavailable for that PR and excluded from PR-size aggregates.

## appsettings.json parameters
All settings are under the `Bitbucket` object.

- `Days` (`int`): Optional rolling lookback window. Used only when `FromDate` and `ToDate` are not configured.
- `FromDate` (`string`): Optional inclusive report start date. Supports `dd.MM.yyyy` and `yyyy-MM-dd`.
- `ToDate` (`string`): Optional inclusive report end date. Supports `dd.MM.yyyy` and `yyyy-MM-dd`.
- `Workspace` (`string`): Bitbucket workspace slug/name to scan repositories from.
- `PageLength` (`int`): API page size for repository/PR/activity requests.
- `PullRequestConcurrency` (`int`): Maximum number of pull requests analyzed in parallel per repository.
- `RepositoryConcurrency` (`int`): Maximum number of repositories analyzed in parallel.
- `MaxRetries` (`int`): Maximum number of retries for retryable Bitbucket API requests such as `429 Too Many Requests`.
- `RetryDelayStepSeconds` (`int`): Base retry backoff step in seconds.
- `MaxRetryDelaySeconds` (`int`): Maximum retry backoff delay in seconds.
- `Username` (`string`): Bitbucket account username/email used for authentication.
- `AppPassword` (`string`): Bitbucket app password used for authentication.
- `RepoSearchMode` (`string` enum):
  - `SearchByFilter`
  - `FilterFromTheList`
- `PrTimeFilterMode` (`string` enum):
  - `LastKnownUpdateAndCreated` (default behavior): stop PR paging when both
    `lastKnownUpdate` and `createdOn` are older than `filterDate`.
  - `CreatedOnOnly`: stop PR paging when `createdOn` is older than `filterDate`.
- `PullRequestSizeMode` (`string` enum):
  - `Lines` (default): PR size metric is `lineChurn = linesAdded + linesRemoved`.
  - `Files`: PR size metric is number of changed files.
- `ExcludeWeekend` (`bool`): Excludes Saturdays and Sundays from time-based metrics (TTFR, time-to-merge, open PR age).
- `ExcludedDays` (`string[]`): Optional explicit holidays/non-working days excluded from time-based metrics.
  Supports `dd.MM.yyyy` and `yyyy-MM-dd` formats.
- `PeopleCsvPath` (`string`): Optional path to a CSV file with developer metadata in format `Name;Grade;Department`.
  Name matching is exact against developer display names in report stats. Missing matches stay `N/A`.
- `TeamFilter` (`string`): Optional team name resolved from the `Department` column in `PeopleCsvPath`. When set, PR rows, PR metrics, and developer output are limited to that team, while team member review activity on other authors' PRs is still counted.
- `ShowDeveloperUuidInStats` (`bool`): Optional flag to include Bitbucket user UUIDs in developer stats output. Default is `false`.
- `ShowAllDetailsForDevelopers` (`bool`): Optional flag to append detailed per-developer sections after the summary report. When enabled, the report includes authored PRs, comments, approvals, and follow-up commits for each developer. Default is `false`.
- `Telemetry.Enabled` (`bool`): Enables/disables Bitbucket request telemetry in the console output. Default is `true`.
- `RepoNameFilter` (`string`): Substring filter used when `RepoSearchMode = SearchByFilter`.
- `RepoNameList` (`string[]`): Explicit repo names/slugs used when `RepoSearchMode = FilterFromTheList`.
- `BranchNameList` (`string[]`): Target branch names to include in PR analysis (e.g., `develop`, `master`).
- `Html.Enabled` (`bool`): Enables/disables HTML generation after console output is rendered.
- `Html.OutputPath` (`string`): Output file path for the HTML report. A date suffix (`dd_MM_yyyy`) is appended automatically.
- `Html.OpenInBrowser` (`bool`): Opens the generated HTML report in the default browser after it is saved.
- `Pdf.Enabled` (`bool`): Enables/disables PDF generation after console output is rendered.
- `Pdf.OutputPath` (`string`): Output file path for the PDF. A date suffix (`dd_MM_yyyy`) is appended automatically.

## Example configuration
```json
{
  "Bitbucket": {
    "Days": 30,
    "FromDate": "",
    "ToDate": "",
    "Workspace": "your-workspace",
    "PageLength": 50,
    "PullRequestConcurrency": 4,
    "RepositoryConcurrency": 4,
    "MaxRetries": 10,
    "RetryDelayStepSeconds": 2,
    "MaxRetryDelaySeconds": 30,
    "Username": "your-email@company.com",
    "AppPassword": "your-app-password",
    "RepoSearchMode": "FilterFromTheList",
    "PrTimeFilterMode": "LastKnownUpdateAndCreated",
    "PullRequestSizeMode": "Lines",
    "ExcludeWeekend": false,
    "ExcludedDays": [
      "01.01.2026",
      "2026-01-02"
    ],
    "PeopleCsvPath": "people.csv",
    "TeamFilter": "",
    "ShowDeveloperUuidInStats": false,
    "ShowAllDetailsForDevelopers": false,
    "Telemetry": {
      "Enabled": true
    },
    "RepoNameFilter": "ABC.",
    "RepoNameList": [
      "Service.A",
      "Service.B"
    ],
    "BranchNameList": [
      "develop"
    ],
    "Html": {
      "Enabled": true,
      "OutputPath": "bbdevpulse-report.html",
      "OpenInBrowser": false
    },
    "Pdf": {
      "Enabled": true,
      "OutputPath": "bbdevpulse-report.pdf"
    }
  }
}
```

For a fixed monthly report, prefer:
```json
{
  "Bitbucket": {
    "FromDate": "2026-02-01",
    "ToDate": "2026-02-28"
  }
}
```

Example `people.csv`:
```text
Name;Grade;Department
Alice Doe;Senior;Core
Bob Smith;Middle;Import
```
## Output

>For demonstration purposes, the program output shown in the screenshots uses synthetic data to avoid exposing information from real repositories and users.

### Console
<img src="Page1.png" alt="BBDevPulse output part 1">
<img src="Page2.png" alt="BBDevPulse output part 2">

### PDF
<img src="Page3.png" alt="BBDevPulse output part 3">
<img src="Page4.png" alt="BBDevPulse output part 4">

### HTML
- Uses the same sections as the PDF report.
- Detailed tables are interactive in the browser and support sorting/filtering.
- Summary stats tables are intentionally static.

using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.API;

/// <summary>
/// Builds Bitbucket pull request list URIs with server-side filters.
/// </summary>
internal sealed class PullRequestsUriBuilder : IPullRequestsUriBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestsUriBuilder"/> class.
    /// </summary>
    /// <param name="uriBuilder">General Bitbucket URI builder.</param>
    /// <param name="options">Bitbucket options.</param>
    public PullRequestsUriBuilder(
        IBitbucketUriBuilder uriBuilder,
        IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(uriBuilder);
        ArgumentNullException.ThrowIfNull(options);
        _uriBuilder = uriBuilder;
        _pageLength = options.Value.PageLength;
        _reportParameters = options.Value.CreateReportParameters();
    }

    /// <inheritdoc />
    public Uri Build(Workspace workspace, RepoSlug repoSlug)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repoSlug);

        var query = BuildPullRequestQuery(_reportParameters);
        var path =
            $"repositories/{workspace.Value}/{repoSlug.Value}/pullrequests?pagelen={_pageLength}" +
            $"&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=-updated_on";

        if (!string.IsNullOrWhiteSpace(query))
        {
            path += $"&q={Uri.EscapeDataString(query)}";
        }

        return _uriBuilder.BuildRelativeUri(path, BitbucketFieldGroup.PullRequestList);
    }

    private static string? BuildPullRequestQuery(ReportParameters reportParameters)
    {
        var clauses = new List<string>(capacity: 3);
        var filterDate = FormatQueryDate(reportParameters.FilterDate);

        switch (reportParameters.PrTimeFilterMode)
        {
            case PrTimeFilterMode.CreatedOnOnly:
                clauses.Add($"created_on >= {filterDate}");
                break;
            case PrTimeFilterMode.LastKnownUpdateAndCreated:
                clauses.Add($"(created_on >= {filterDate} OR updated_on >= {filterDate})");
                break;
            default:
                throw new NotImplementedException();
        }

        var branchFilterClause = BuildBranchFilterClause(reportParameters.BranchNameList);
        if (!string.IsNullOrWhiteSpace(branchFilterClause))
        {
            clauses.Add(branchFilterClause);
        }

        if (reportParameters.ToDateExclusive.HasValue)
        {
            clauses.Add($"created_on < {FormatQueryDate(reportParameters.ToDateExclusive.Value)}");
        }

        return clauses.Count == 0
            ? null
            : string.Join(" AND ", clauses);
    }

    private static string? BuildBranchFilterClause(IReadOnlyList<BranchName> branchNameList)
    {
        ArgumentNullException.ThrowIfNull(branchNameList);

        if (branchNameList.Count == 0)
        {
            return null;
        }

        var branchClauses = branchNameList
            .Select(static branch => $"destination.branch.name = \"{EscapeQueryStringLiteral(branch.Value)}\"")
            .ToArray();

        return branchClauses.Length == 1
            ? branchClauses[0]
            : $"({string.Join(" OR ", branchClauses)})";
    }

    private static string EscapeQueryStringLiteral(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string FormatQueryDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private readonly IBitbucketUriBuilder _uriBuilder;
    private readonly int _pageLength;
    private readonly ReportParameters _reportParameters;
}

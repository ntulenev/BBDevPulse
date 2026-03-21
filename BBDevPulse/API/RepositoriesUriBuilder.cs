using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.API;

/// <summary>
/// Builds Bitbucket repository list URIs with server-side filters.
/// </summary>
internal sealed class RepositoriesUriBuilder : IRepositoriesUriBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoriesUriBuilder"/> class.
    /// </summary>
    /// <param name="uriBuilder">General Bitbucket URI builder.</param>
    /// <param name="options">Bitbucket options.</param>
    public RepositoriesUriBuilder(
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
    public Uri Build(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var path = $"repositories/{workspace.Value}?pagelen={_pageLength}";
        var query = BuildRepositoryQuery(_reportParameters);
        if (!string.IsNullOrWhiteSpace(query))
        {
            path += $"&q={Uri.EscapeDataString(query)}";
        }

        return _uriBuilder.BuildRelativeUri(path, BitbucketFieldGroup.RepositoryList);
    }

    private static string? BuildRepositoryQuery(ReportParameters reportParameters)
    {
        ArgumentNullException.ThrowIfNull(reportParameters);

        return reportParameters.RepoSearchMode switch
        {
            RepoSearchMode.SearchByFilter => BuildSearchByFilterQuery(reportParameters.RepoNameFilter.Value),
            RepoSearchMode.FilterFromTheList => BuildFilterFromTheListQuery(reportParameters.RepoNameList),
            _ => throw new NotImplementedException()
        };
    }

    private static string? BuildSearchByFilterQuery(string filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterValue))
        {
            return null;
        }

        var escapedFilter = EscapeQueryStringLiteral(filterValue);
        return $"(name ~ \"{escapedFilter}\" OR slug ~ \"{escapedFilter}\")";
    }

    private static string? BuildFilterFromTheListQuery(IReadOnlyList<RepoName> repoNameList)
    {
        ArgumentNullException.ThrowIfNull(repoNameList);

        if (repoNameList.Count == 0)
        {
            return null;
        }

        var repoClauses = repoNameList
            .Select(static repoName => repoName.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(static repoValue =>
            {
                var escapedValue = EscapeQueryStringLiteral(repoValue);
                return new[]
                {
                    $"name = \"{escapedValue}\"",
                    $"slug = \"{escapedValue}\""
                };
            })
            .ToArray();

        return $"({string.Join(" OR ", repoClauses)})";
    }

    private static string EscapeQueryStringLiteral(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private readonly IBitbucketUriBuilder _uriBuilder;
    private readonly int _pageLength;
    private readonly ReportParameters _reportParameters;
}

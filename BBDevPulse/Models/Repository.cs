namespace BBDevPulse.Models;

/// <summary>
/// Bitbucket repository domain model.
/// </summary>
public sealed class Repository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Repository"/> class.
    /// </summary>
    /// <param name="name">Repository name.</param>
    /// <param name="slug">Repository slug.</param>
    public Repository(RepoName name, RepoSlug slug)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(slug);

        Name = name;
        Slug = slug;
    }

    /// <summary>
    /// Repository name.
    /// </summary>
    public RepoName Name { get; }

    /// <summary>
    /// Repository slug.
    /// </summary>
    public RepoSlug Slug { get; }

    /// <summary>
    /// Repository name fallback to slug for reporting.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name.Value) ? Slug.Value : Name.Value;

    /// <summary>
    /// Determines whether the repository matches the filter criteria.
    /// </summary>
    /// <param name="searchMode">Repository search mode.</param>
    /// <param name="filter">Repository name filter.</param>
    /// <param name="repoList">Explicit repository list filter.</param>
    /// <returns>True when the repository should be included.</returns>
    public bool MatchesFilter(
        RepoSearchMode searchMode,
        RepoNameFilter filter,
        IReadOnlyList<RepoName> repoList)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(repoList);

        var name = Name.Value;
        var slug = Slug.Value;
        var filterValue = filter.Value;

        return searchMode switch
        {
            RepoSearchMode.FilterFromTheList => repoList.Count == 0 ||
                repoList.Any(entry =>
                    name.Equals(entry.Value, StringComparison.OrdinalIgnoreCase) ||
                    slug.Equals(entry.Value, StringComparison.OrdinalIgnoreCase)),
            RepoSearchMode.SearchByFilter => throw new NotImplementedException(),
            _ => string.IsNullOrWhiteSpace(filterValue) ||
                            name.Contains(filterValue, StringComparison.OrdinalIgnoreCase) ||
                            slug.Contains(filterValue, StringComparison.OrdinalIgnoreCase)
        };
    }
}

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
}

using System.Globalization;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console repository list presenter.
/// </summary>
public sealed class SpectreRepositoryListPresenter : IRepositoryListPresenter
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Repository>> FetchRepositoriesAsync(
        Func<Action<int>, CancellationToken, IAsyncEnumerable<Repository>> fetchRepositories,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fetchRepositories);
        var repositories = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Loading repositories...", async _ =>
            {
                var result = new List<Repository>();
                await foreach (var repo in fetchRepositories(
                                   page => AnsiConsole.MarkupLine($"[grey]Loading repositories: page {page}[/]"),
                                   cancellationToken).ConfigureAwait(false))
                {
                    result.Add(repo);
                }

                return result;
            })
            .ConfigureAwait(false);

        return [.. repositories];
    }

    /// <inheritdoc />
    public void RenderRepositoryTable(
        IReadOnlyCollection<Repository> repositories,
        RepoSearchMode searchMode,
        RepoNameFilter filter,
        IReadOnlyList<RepoName> repoList)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(repoList);
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Repository")
            .AddColumn("Slug");

        var index = 1;
        foreach (var repo in repositories.OrderBy(repo => repo.DisplayName))
        {
            _ = table.AddRow(
                index.ToString(CultureInfo.InvariantCulture),
                repo.DisplayName,
                repo.Slug.Value);
            index++;
        }

        var title = searchMode switch
        {
            RepoSearchMode.FilterFromTheList when repoList.Count > 0
                => $"Repositories (list count: {repoList.Count})",
            RepoSearchMode.SearchByFilter when !string.IsNullOrWhiteSpace(filter.Value)
                => $"Repositories (contains: {filter.Value})",
            _ => "Repositories (all)"
        };

        AnsiConsole.Write(new Rule(title).RuleStyle("grey"));
        AnsiConsole.Write(table);
    }
}

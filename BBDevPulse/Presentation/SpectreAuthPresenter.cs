using BBDevPulse.Abstractions;
using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Spectre.Console authentication presenter.
/// </summary>
public sealed class SpectreAuthPresenter : IAuthPresenter
{
    /// <inheritdoc />
    public async Task AnnounceAuthAsync(Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fetchUser);
        AnsiConsole.MarkupLine("[grey]Authenticating with Bitbucket...[/]");
        try
        {
            var user = await fetchUser(cancellationToken).ConfigureAwait(false);
            var name = user.DisplayName.Value;
            AnsiConsole.MarkupLine($"[green]Auth succeeded for user:[/] {name}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Auth failed:[/] {ex.Message}");
            throw;
        }
    }
}

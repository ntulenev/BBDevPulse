using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Displays the application identity before any startup prompts or report output.
/// </summary>
internal static class SpectreStartupPresenter
{
    public static void Render()
    {
        // A small chart and heartbeat echo the repository logo without relying on image support.
        AnsiConsole.MarkupLine("[green]   ▂  ▄  ▆  █          ╱╲[/]");
        AnsiConsole.MarkupLine("[green]   █  █  █  █  ───────╯  ╲  ╱╲[/]");
        AnsiConsole.MarkupLine("[green]   █  █  █  █              ╲╱  ╲────[/]");
        AnsiConsole.MarkupLine("[bold green]   BBDevPulse[/]");
        AnsiConsole.MarkupLine("[grey]   Bitbucket pull requests, reviews, and developer activity.[/]");
        AnsiConsole.MarkupLine("[grey]   Explore metrics in the console or export HTML and PDF reports.[/]");
        AnsiConsole.WriteLine();
    }
}

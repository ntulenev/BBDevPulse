using Spectre.Console;

namespace BBDevPulse.Presentation;

/// <summary>
/// Displays the application identity before any startup prompts or report output.
/// </summary>
internal static class SpectreStartupPresenter
{
    private const int WideLayoutWidth = 80;

    public static void Render()
    {
        var width = AnsiConsole.Profile.Width;

        if (width >= WideLayoutWidth)
        {
            // Bars and a heartbeat are the two distinctive shapes in the repository logo.
            AnsiConsole.MarkupLine("[green]    ▂   ▄   ▆   █             ╱╲[/]");
            AnsiConsole.MarkupLine("[green]    █   █   █   █   ─────────╯  ╲  ╱╲[/]");
            AnsiConsole.MarkupLine("[green]    █   █   █   █                 ╲╱  ╲────[/]");
            AnsiConsole.MarkupLine("[green]    ────────────────────────────────────────[/]");
            AnsiConsole.Write(new FigletText("BBDevPulse").Color(Color.Green));
            AnsiConsole.MarkupLine("[grey]    Bitbucket pull requests, reviews, and developer activity.[/]");
            AnsiConsole.MarkupLine("[grey]    Console metrics and HTML/PDF reports.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]  ▂ ▄ ▆ █    ───╱╲╱╲───[/]");
            AnsiConsole.MarkupLine("[bold green]  BBDevPulse[/]");
            AnsiConsole.MarkupLine("[grey]  Bitbucket PR and review analytics.[/]");
            AnsiConsole.MarkupLine("[grey]  Console, HTML and PDF reports.[/]");
        }

        AnsiConsole.WriteLine();
    }
}

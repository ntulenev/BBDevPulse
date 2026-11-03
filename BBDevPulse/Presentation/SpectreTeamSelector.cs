using BBDevPulse.Models;

using Spectre.Console;

namespace BBDevPulse.Presentation;

internal static class SpectreTeamSelector
{
    internal static string? SelectTeamFilter(IReadOnlyList<TeamFilterOption> teams, int totalPeople)
    {
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentOutOfRangeException.ThrowIfNegative(totalPeople);
        if (teams.Count == 0)
        {
            return null;
        }

        var choices = new List<TeamChoice> { new("Do not apply team filter", null) };
        choices.AddRange(teams.Select(team => new TeamChoice(
            $"{team.Name} ({CountLabel(team.Members.Count)})", team)));
        var prompt = new SelectionPrompt<TeamChoice>()
            .Title("Filter report by team?")
            .PageSize(10)
            .UseConverter(choice => Markup.Escape(choice.Label))
            .AddChoices(choices);
        var selected = AnsiConsole.Prompt(prompt).Team;
        if (selected is not null)
        {
            ShowTeamMembers(selected);
        }

        return selected?.Name;
    }

    private sealed record TeamChoice(string Label, TeamFilterOption? Team);

    private sealed record PageChoice(string Label, int? Page);

    private static string CountLabel(int count) => count == 1 ? "1 person" : $"{count} people";

    private static void ShowTeamMembers(TeamFilterOption team)
    {
        const int pageSize = 12;
        var pageCount = (team.Members.Count + pageSize - 1) / pageSize;
        var page = 0;
        while (true)
        {
            AnsiConsole.Write(new Rule($"[bold deepskyblue1]{Markup.Escape(team.Name)} · {CountLabel(team.Members.Count)}[/]")
                .LeftJustified().RuleStyle("grey"));
            AnsiConsole.MarkupLine($"[grey]People in selected team · page {page + 1} of {pageCount}[/]");
            var table = new Table().Border(TableBorder.Rounded).Expand();
            _ = table.AddColumn("Name");
            _ = table.AddColumn("Grade");
            foreach (var member in team.Members.Skip(page * pageSize).Take(pageSize))
            {
                _ = table.AddRow(Markup.Escape(member.Name), Markup.Escape(member.Grade));
            }

            AnsiConsole.Write(table);
            if (pageCount == 1)
            {
                return;
            }

            var choices = new List<PageChoice>();
            if (page > 0)
            {
                choices.Add(new("Previous page", page - 1));
            }

            if (page + 1 < pageCount)
            {
                choices.Add(new("Next page", page + 1));
            }

            choices.Add(new("Done reviewing", null));
            var navigation = new SelectionPrompt<PageChoice>()
                .Title("Review team members")
                .UseConverter(static choice => choice.Label)
                .AddChoices(choices);
            var nextPage = AnsiConsole.Prompt(navigation).Page;
            if (!nextPage.HasValue)
            {
                return;
            }

            page = nextPage.Value;
        }
    }
}

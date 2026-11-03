using BBDevPulse.Models;

namespace BBDevPulse.Logic;

internal static class TeamFilterCatalog
{
    public static List<TeamFilterOption> Create(IReadOnlyDictionary<DisplayName, PersonCsvRow> peopleByName) =>
        peopleByName
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Value.Department) &&
                !string.Equals(entry.Value.Department.Trim(), DeveloperStats.NOT_AVAILABLE, StringComparison.OrdinalIgnoreCase))
            .GroupBy(static entry => entry.Value.Department.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(static group => new TeamFilterOption(
                group.Key,
                group.Select(static entry => new TeamMember(entry.Key.Value, entry.Value.Grade))
                    .OrderBy(static member => member.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderBy(static team => team.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

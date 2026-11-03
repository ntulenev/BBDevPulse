namespace BBDevPulse.Models;

/// <summary>
/// A team available for filtering and its people from the CSV file.
/// </summary>
/// <param name="Name">Team name.</param>
/// <param name="Members">People assigned to the team.</param>
public sealed record TeamFilterOption(string Name, IReadOnlyList<TeamMember> Members);

/// <summary>
/// A person shown in the team preview.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Grade">Grade from the people CSV.</param>
public sealed record TeamMember(string Name, string Grade);

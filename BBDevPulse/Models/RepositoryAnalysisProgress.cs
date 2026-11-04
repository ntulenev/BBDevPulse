namespace BBDevPulse.Models;

/// <summary>Progress of repository collection.</summary>
/// <param name="RepositoryName">Repository being started, or null after completion.</param>
/// <param name="Completed">Number of finished repositories.</param>
/// <param name="Active">Number of active repositories.</param>
/// <param name="Total">Number of scheduled repositories.</param>
public sealed record RepositoryAnalysisProgress(string? RepositoryName, int Completed, int Active, int Total);

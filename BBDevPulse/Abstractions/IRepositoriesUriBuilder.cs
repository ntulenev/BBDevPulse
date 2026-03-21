using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Builds Bitbucket repository list URIs with server-side filters.
/// </summary>
internal interface IRepositoriesUriBuilder
{
    /// <summary>
    /// Builds the repository list URI for the provided workspace.
    /// </summary>
    /// <param name="workspace">Workspace identifier.</param>
    /// <returns>Relative URI for the repository list endpoint.</returns>
    Uri Build(Workspace workspace);
}

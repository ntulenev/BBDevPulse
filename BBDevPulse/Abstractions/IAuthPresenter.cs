using BBDevPulse.Models;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Presents authentication status.
/// </summary>
public interface IAuthPresenter
{
    /// <summary>
    /// Displays authentication status using the provided user fetcher.
    /// </summary>
    /// <param name="fetchUser">Function to retrieve the authenticated user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AnnounceAuthAsync(Func<CancellationToken, Task<AuthUser>> fetchUser, CancellationToken cancellationToken);
}

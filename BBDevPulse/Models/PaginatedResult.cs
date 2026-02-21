namespace BBDevPulse.Models;

/// <summary>
/// Pagination result with items and the next page URI.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    Uri? NextPage);

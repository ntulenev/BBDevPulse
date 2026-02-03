using System.Runtime.CompilerServices;

namespace BBDevPulse.Abstractions;

/// <summary>
/// Provides pagination helpers for reading items from paged APIs.
/// </summary>
internal interface IPaginatorHelper
{
    /// <summary>
    /// Reads all items from a paged API until no next page is available.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="firstPage">URI of the first page.</param>
    /// <param name="getPageAsync">Callback to fetch a page.</param>
    /// <param name="onPage">Optional callback invoked with 1-based page index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream of items across all pages.</returns>
    IAsyncEnumerable<T> ReadAllAsync<T>(
        Uri firstPage,
        Func<Uri, CancellationToken, Task<PaginatedResult<T>>> getPageAsync,
        Action<int>? onPage,
#pragma warning disable CS8424 
        [EnumeratorCancellation] CancellationToken cancellationToken);
#pragma warning restore CS8424
}

/// <summary>
/// Pagination result with items and the next page URI.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    Uri? NextPage);

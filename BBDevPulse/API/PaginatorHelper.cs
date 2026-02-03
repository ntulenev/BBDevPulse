using System.Runtime.CompilerServices;

using BBDevPulse.Abstractions;

namespace BBDevPulse.API;

/// <summary>
/// Default pagination helper.
/// </summary>
internal sealed class PaginatorHelper : IPaginatorHelper
{
    /// <inheritdoc />
    public async IAsyncEnumerable<T> ReadAllAsync<T>(
        Uri firstPage,
        Func<Uri, CancellationToken, Task<PaginatedResult<T>>> getPageAsync,
        Action<int>? onPage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(firstPage);
        ArgumentNullException.ThrowIfNull(getPageAsync);

        var next = firstPage;
        var pageIndex = 0;

        while (next is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageIndex++;
            onPage?.Invoke(pageIndex);

            var page = await getPageAsync(next, cancellationToken)
                .ConfigureAwait(false);
            var items = page.Items ?? [];

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }

            next = page.NextPage;
        }
    }
}

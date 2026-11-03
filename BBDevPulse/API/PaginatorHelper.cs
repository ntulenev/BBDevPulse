using System.Runtime.CompilerServices;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.API;

/// <summary>
/// Default pagination helper.
/// </summary>
internal sealed class PaginatorHelper : IPaginatorHelper
{
    public PaginatorHelper(Uri? baseAddress = null)
    {
        _baseAddress = baseAddress ?? new Uri("https://api.bitbucket.org/2.0/");
        if (!_baseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("Pagination base address must be absolute.", nameof(baseAddress));
        }
    }

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
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (next is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri resolved;
            try
            {
                resolved = new Uri(_baseAddress, next);
            }
            catch (UriFormatException exception)
            {
                throw new InvalidOperationException("Invalid Bitbucket pagination URI.", exception);
            }

            if (!visited.Add(resolved.AbsoluteUri))
            {
                throw new InvalidOperationException("Bitbucket pagination contains a repeated page.");
            }

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

    private readonly Uri _baseAddress;
}

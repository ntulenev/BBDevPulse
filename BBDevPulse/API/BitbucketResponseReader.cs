using BBDevPulse.Abstractions;
using BBDevPulse.Models;
using BBDevPulse.Transport;

namespace BBDevPulse.API;

internal sealed class BitbucketResponseReader
{
    private readonly IBitbucketTransport _transport;
    private readonly IPaginatorHelper _paginator;

    public BitbucketResponseReader(IBitbucketTransport transport, IPaginatorHelper paginatorHelper)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(paginatorHelper);
        _transport = transport;
        _paginator = paginatorHelper;
    }

    public Task<T> GetAsync<T>(Uri uri, CancellationToken cancellationToken) =>
        _transport.GetAsync<T>(uri, cancellationToken);

    public IAsyncEnumerable<T> ReadAllAsync<T>(Uri firstPage, Action<int>? onPage, CancellationToken cancellationToken) =>
        _paginator.ReadAllAsync(firstPage, ReadPageAsync<T>, onPage, cancellationToken);

    private async Task<PaginatedResult<T>> ReadPageAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        var page = await GetAsync<PaginatedResponse<T>>(uri, cancellationToken).ConfigureAwait(false);
        var next = string.IsNullOrWhiteSpace(page.Next) ? null : new Uri(page.Next, UriKind.RelativeOrAbsolute);
        return new PaginatedResult<T>(page.Values ?? [], next);
    }
}

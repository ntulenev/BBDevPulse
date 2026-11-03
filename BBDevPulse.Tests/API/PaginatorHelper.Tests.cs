using FluentAssertions;

using BBDevPulse.API;
using BBDevPulse.Models;

namespace BBDevPulse.Tests.API;

public sealed class PaginatorHelperTests
{
    [Fact]
    public async Task ReadAllAsyncDetectsAbsoluteReturnToRelativeFirstPage()
    {
        var first = new Uri("page/a?cursor=1", UriKind.Relative);
        var second = new Uri("https://example.test/api/page/b?cursor=2");
        var repeatedFirst = new Uri("https://example.test/api/page/a?cursor=1");
        var helper = new PaginatorHelper(new Uri("https://example.test/api/"));
        var fetches = 0;

        async Task ReadAsync()
        {
            await foreach (var _ in helper.ReadAllAsync(first, (uri, _) =>
            {
                fetches++;
                return Task.FromResult(new PaginatedResult<int>([fetches],
                    fetches == 1 ? second : repeatedFirst));
            }, null, CancellationToken.None))
            {
            }
        }

        await FluentActions.Invoking(ReadAsync).Should().ThrowAsync<InvalidOperationException>();
        fetches.Should().Be(2);
    }

    [Fact]
    public async Task ReadAllAsyncRejectsRepeatedPageBeforeThirdFetch()
    {
        var first = new Uri("https://example.test/page/a?cursor=1");
        var second = new Uri("https://example.test/page/b?cursor=2");
        var fetches = 0;
        var helper = new PaginatorHelper();

        async Task ReadAsync()
        {
            await foreach (var _ in helper.ReadAllAsync(first, (uri, _) =>
            {
                fetches++;
                return Task.FromResult(new PaginatedResult<int>([fetches], uri == first ? second : first));
            }, null, CancellationToken.None))
            {
            }
        }

        await FluentActions.Invoking(ReadAsync).Should().ThrowAsync<InvalidOperationException>();
        fetches.Should().Be(2);
    }

    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "ReadAllAsync throws when first page is null")]
    [Trait("Category", "Unit")]
    public async Task ReadAllAsyncWhenFirstPageIsNullThrowsArgumentNullException()
    {
        // Arrange
        var helper = new PaginatorHelper();
        Uri firstPage = null!;
        Func<Uri, CancellationToken, Task<PaginatedResult<int>>> getPageAsync =
            (_, _) => Task.FromResult(new PaginatedResult<int>([], null));

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in helper.ReadAllAsync(firstPage, getPageAsync, null, cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "ReadAllAsync throws when page callback is null")]
    [Trait("Category", "Unit")]
    public async Task ReadAllAsyncWhenGetPageCallbackIsNullThrowsArgumentNullException()
    {
        // Arrange
        var helper = new PaginatorHelper();
        Func<Uri, CancellationToken, Task<PaginatedResult<int>>> getPageAsync = null!;

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in helper.ReadAllAsync(new Uri("https://example.test/page/1"), getPageAsync, null, cancellationToken))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "ReadAllAsync streams all items across pages and invokes page callback")]
    [Trait("Category", "Unit")]
    public async Task ReadAllAsyncWhenMultiplePagesExistReturnsFlattenedItems()
    {
        // Arrange
        var helper = new PaginatorHelper();
        var pagesVisited = new List<Uri>();
        var pageIndexes = new List<int>();
        var first = new Uri("https://example.test/page/1");
        var second = new Uri("https://example.test/page/2");

        Task<PaginatedResult<int>> GetPageAsync(Uri uri, CancellationToken _)
        {
            pagesVisited.Add(uri);
            return uri == first
                ? Task.FromResult(new PaginatedResult<int>([1, 2], second))
                : Task.FromResult(new PaginatedResult<int>([3], null));
        }

        // Act
        var items = await ReadAllAsync(
            helper.ReadAllAsync(first, GetPageAsync, page => pageIndexes.Add(page), cancellationToken));

        // Assert
        items.Should().Equal(1, 2, 3);
        pagesVisited.Should().Equal(first, second);
        pageIndexes.Should().Equal(1, 2);
    }

    [Fact(DisplayName = "ReadAllAsync handles null items list as empty page")]
    [Trait("Category", "Unit")]
    public async Task ReadAllAsyncWhenPageItemsAreNullTreatsThemAsEmpty()
    {
        // Arrange
        var helper = new PaginatorHelper();
        var first = new Uri("https://example.test/page/1");

        // Act
        var items = await ReadAllAsync(helper.ReadAllAsync(
            first,
            (_, _) => Task.FromResult(new PaginatedResult<int>(null!, null)),
            onPage: null,
            cancellationToken));

        // Assert
        items.Should().BeEmpty();
    }

    [Fact(DisplayName = "ReadAllAsync throws when cancellation is requested before first page fetch")]
    [Trait("Category", "Unit")]
    public async Task ReadAllAsyncWhenTokenAlreadyCanceledThrowsOperationCanceledException()
    {
        // Arrange
        var helper = new PaginatorHelper();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var callbackCalled = false;

        Task<PaginatedResult<int>> GetPageAsync(Uri _, CancellationToken __)
        {
            callbackCalled = true;
            return Task.FromResult(new PaginatedResult<int>([1], null));
        }

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var _ in helper.ReadAllAsync(
                               new Uri("https://example.test/page/1"),
                               GetPageAsync,
                               onPage: null,
                               cts.Token))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        callbackCalled.Should().BeFalse();
    }

    [Fact(DisplayName = "ReadAllAsync checks cancellation while streaming page items")]
    [Trait("Category", "Unit")]
    public async Task ReadAllAsyncWhenCanceledDuringIterationThrowsOperationCanceledException()
    {
        // Arrange
        var helper = new PaginatorHelper();
        using var cts = new CancellationTokenSource();
        var first = new Uri("https://example.test/page/1");

        // Act
        Func<Task> act = async () =>
        {
            await foreach (var item in helper.ReadAllAsync(
                               first,
                               (_, _) => Task.FromResult(new PaginatedResult<int>([1, 2], null)),
                               onPage: null,
                               cts.Token))
            {
                if (item == 1)
                {
                    cts.Cancel();
                }
            }
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task<List<T>> ReadAllAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }
}

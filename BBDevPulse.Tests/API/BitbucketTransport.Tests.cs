using System.Net;
using System.Net.Http;
using System.Text;

using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.API;

using Moq;

namespace BBDevPulse.Tests.API;

public sealed class BitbucketTransportTests
{
    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "Constructor throws when HTTP client is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenHttpClientIsNullThrowsArgumentNullException()
    {
        // Arrange
        HttpClient httpClient = null!;
        var retryPolicyHelper = new Mock<IRetryPolicyHelper>(MockBehavior.Strict).Object;

        // Act
        Action act = () => _ = new BitbucketTransport(httpClient, retryPolicyHelper);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when retry policy helper is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRetryPolicyHelperIsNullThrowsArgumentNullException()
    {
        // Arrange
        IRetryPolicyHelper retryPolicyHelper = null!;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        // Act
        Action act = () => _ = new BitbucketTransport(httpClient, retryPolicyHelper);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetAsync returns deserialized payload for successful response")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenResponseIsSuccessfulReturnsDeserializedValue()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"displayname":"Alice"}""", Encoding.UTF8, "application/json")
            });
        using var httpClient = new HttpClient(handler);
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper());

        // Act
        var result = await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), cancellationToken);

        // Assert
        result.DisplayName.Should().Be("Alice");
    }

    [Fact(DisplayName = "GetAsync throws when response status is not successful")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenResponseIsNotSuccessfulThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("invalid request", Encoding.UTF8, "text/plain")
            });
        using var httpClient = new HttpClient(handler);
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper());

        // Act
        Func<Task> act = async () =>
            _ = await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), cancellationToken);

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("BadRequest");
        exception.Which.Message.Should().Contain("invalid request");
    }

    [Fact(DisplayName = "GetAsync throws when payload deserializes to null")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenPayloadIsNullThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            });
        using var httpClient = new HttpClient(handler);
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper());

        // Act
        Func<Task> act = async () =>
            _ = await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), cancellationToken);

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("empty");
    }

    [Fact(DisplayName = "GetAsync retries when Bitbucket returns too many requests and eventually succeeds")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenTooManyRequestsReturnedRetriesAndSucceeds()
    {
        // Arrange
        var callCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"displayname":"Alice"}""", Encoding.UTF8, "application/json")
                };
        });
        using var httpClient = new HttpClient(handler);
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper());

        // Act
        var result = await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), cancellationToken);

        // Assert
        callCount.Should().Be(2);
        result.DisplayName.Should().Be("Alice");
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private sealed class SampleDto
    {
        public string? DisplayName { get; init; }
    }

    private static IRetryPolicyHelper CreatePassthroughRetryPolicyHelper()
    {
        var retryPolicyHelper = new Mock<IRetryPolicyHelper>(MockBehavior.Strict);
        retryPolicyHelper
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task<SampleDto>>>(),
                It.IsAny<Func<Exception, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<SampleDto>>, Func<Exception, bool>, CancellationToken>(static async (operation, shouldRetry, token) =>
            {
                while (true)
                {
                    try
                    {
                        return await operation(token);
                    }
                    catch (Exception ex) when (shouldRetry(ex))
                    {
                    }
                }
            });

        return retryPolicyHelper.Object;
    }
}

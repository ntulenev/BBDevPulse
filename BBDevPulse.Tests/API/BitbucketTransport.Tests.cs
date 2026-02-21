using System.Net;
using System.Net.Http;
using System.Text;

using FluentAssertions;

using BBDevPulse.API;

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

        // Act
        Action act = () => _ = new BitbucketTransport(httpClient);

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
        var transport = new BitbucketTransport(httpClient);

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
        var transport = new BitbucketTransport(httpClient);

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
        var transport = new BitbucketTransport(httpClient);

        // Act
        Func<Task> act = async () =>
            _ = await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), cancellationToken);

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("empty");
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
}

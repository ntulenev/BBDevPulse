using System.Net;
using System.Net.Http;
using System.Text;

using FluentAssertions;

using BBDevPulse.Abstractions;
using BBDevPulse.API;
using BBDevPulse.Configuration;

using Microsoft.Extensions.Options;

using Moq;

namespace BBDevPulse.Tests.API;

public sealed class BitbucketTransportTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetAsyncDoesNotRetryAuthenticationFailures(HttpStatusCode status)
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(status) { Content = new StringContent("auth failed") };
        }));
        var transport = new BitbucketTransport(client, CreatePassthroughRetryPolicyHelper(), CreateTelemetryService().Object);

        Func<Task> act = () => transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData("https://other.example/2.0/user")]
    [InlineData("http://api.bitbucket.org/2.0/user")]
    [InlineData("https://api.bitbucket.org:444/2.0/user")]
    [InlineData("//other.example/2.0/user")]
    public async Task GetAsyncRejectsForeignOriginBeforeTransport(string next)
    {
        var sent = false;
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            sent = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        { BaseAddress = new Uri("https://api.bitbucket.org/2.0/") };
        var transport = new BitbucketTransport(client, CreatePassthroughRetryPolicyHelper(), CreateTelemetryService().Object);

        Func<Task> act = () => transport.GetAsync<SampleDto>(new Uri(next, UriKind.RelativeOrAbsolute), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        sent.Should().BeFalse();
    }

    private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

    [Fact(DisplayName = "Constructor throws when HTTP client is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenHttpClientIsNullThrowsArgumentNullException()
    {
        // Arrange
        HttpClient httpClient = null!;
        var retryPolicyHelper = new Mock<IRetryPolicyHelper>(MockBehavior.Strict).Object;
        var telemetryService = new Mock<IBitbucketTelemetryService>(MockBehavior.Strict).Object;

        // Act
        Action act = () => _ = new BitbucketTransport(httpClient, retryPolicyHelper, telemetryService);

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
        var telemetryService = new Mock<IBitbucketTelemetryService>(MockBehavior.Strict).Object;

        // Act
        Action act = () => _ = new BitbucketTransport(httpClient, retryPolicyHelper, telemetryService);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when telemetry service is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTelemetryServiceIsNullThrowsArgumentNullException()
    {
        // Arrange
        IBitbucketTelemetryService telemetryService = null!;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        // Act
        Action act = () => _ = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper(), telemetryService);

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
        var telemetryService = CreateTelemetryService();
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper(), telemetryService.Object);

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
        var telemetryService = CreateTelemetryService();
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper(), telemetryService.Object);

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
        var telemetryService = CreateTelemetryService();
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper(), telemetryService.Object);

        // Act
        Func<Task> act = async () =>
            _ = await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), cancellationToken);

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("empty");
    }

    [Theory(DisplayName = "GetAsync retries transient responses and eventually succeeds")]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenTransientStatusReturnedRetriesAndSucceeds(HttpStatusCode status)
    {
        // Arrange
        var callCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(status)
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"displayname":"Alice"}""", Encoding.UTF8, "application/json")
                };
        });
        using var httpClient = new HttpClient(handler);
        var telemetryService = CreateTelemetryService(expectedCalls: 2);
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper(), telemetryService.Object);

        // Act
        var result = await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), cancellationToken);

        // Assert
        callCount.Should().Be(2);
        result.DisplayName.Should().Be("Alice");
    }

    [Fact(DisplayName = "GetAsync tracks telemetry for each request attempt")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenCalledTracksRequestTelemetry()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"displayname":"Alice"}""", Encoding.UTF8, "application/json")
            });
        using var httpClient = new HttpClient(handler);
        var telemetryService = CreateTelemetryService();
        var transport = new BitbucketTransport(httpClient, CreatePassthroughRetryPolicyHelper(), telemetryService.Object);
        var url = new Uri("https://example.test/user");

        // Act
        var result = await transport.GetAsync<SampleDto>(url, cancellationToken);

        // Assert
        result.DisplayName.Should().Be("Alice");
        telemetryService.Verify(x => x.TrackRequest(url), Times.Once);
    }

    [Fact]
    public async Task GetAsyncRetriesConnectionFailureAndSucceeds()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            if (++calls == 1)
                throw new HttpRequestException("Connection reset");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"displayname\":\"Alice\"}") };
        }));
        var telemetry = CreateTelemetryService();
        var transport = new BitbucketTransport(client, CreateRealRetryPolicy(1), telemetry.Object);

        var result = await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), CancellationToken.None);

        result.DisplayName.Should().Be("Alice");
        calls.Should().Be(2);
        telemetry.Verify(x => x.TrackRequest(It.IsAny<Uri>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsyncStopsAfterRetryBudgetIsExhausted()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            calls++;
            throw new HttpRequestException("Connection reset");
        }));
        var transport = new BitbucketTransport(client, CreateRealRetryPolicy(1), CreateTelemetryService().Object);

        Func<Task> act = () => transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetAsyncRetriesHttpClientTimeout()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            if (++calls == 1)
                throw new TaskCanceledException("Timed out", new TimeoutException());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }));
        var transport = new BitbucketTransport(client, CreateRealRetryPolicy(1), CreateTelemetryService().Object);

        await transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), CancellationToken.None);

        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetAsyncDoesNotRetryCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            calls++;
            cancellation.Cancel();
            throw new TaskCanceledException("Cancelled", new TimeoutException());
        }));
        var transport = new BitbucketTransport(client, CreateRealRetryPolicy(2), CreateTelemetryService().Object);

        Func<Task> act = () => transport.GetAsync<SampleDto>(new Uri("https://example.test/user"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(1);
    }

    private static RetryPolicyHelper CreateRealRetryPolicy(int retries) => new(Options.Create(new BitbucketOptions
    {
        Workspace = "ws",
        PageLength = 25,
        Username = "user",
        AppPassword = "pass",
        RepoNameFilter = "",
        RepoNameList = [],
        BranchNameList = [],
        RepoSearchMode = BBDevPulse.Models.RepoSearchMode.FilterFromTheList,
        PrTimeFilterMode = BBDevPulse.Models.PrTimeFilterMode.CreatedOnOnly,
        MaxRetries = retries,
        RetryDelayStepSeconds = 1,
        MaxRetryDelaySeconds = 1
    }));

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

    private static Mock<IBitbucketTelemetryService> CreateTelemetryService(int expectedCalls = 1)
    {
        var telemetryService = new Mock<IBitbucketTelemetryService>(MockBehavior.Strict);
        telemetryService.Setup(x => x.TrackRequest(It.IsAny<Uri>()));
        return telemetryService;
    }
}

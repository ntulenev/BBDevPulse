using System.Net;

using BBDevPulse.Abstractions;
using BBDevPulse.API;
using BBDevPulse.Configuration;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

namespace BBDevPulse.Tests.API;

public sealed class BitbucketResponseReaderTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"values\":null}")]
    public async Task ReadAllRejectsMissingOrNullValues(string json)
    {
        using var http = new HttpClient(new JsonHandler(json));
        var reader = CreateReader(http);

        Func<Task> act = async () =>
        {
            await foreach (var _ in reader.ReadAllAsync<int>(new Uri("https://example.test/page"), null, CancellationToken.None))
            { }
        };

        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*values array*");
    }

    [Fact]
    public async Task ReadAllAcceptsEmptyValuesArray()
    {
        using var http = new HttpClient(new JsonHandler("{\"values\":[]}"));
        var reader = CreateReader(http);
        var items = new List<int>();

        await foreach (var item in reader.ReadAllAsync<int>(new Uri("https://example.test/page"), null, CancellationToken.None))
            items.Add(item);

        items.Should().BeEmpty();
    }

    private static BitbucketResponseReader CreateReader(HttpClient http)
    {
        var telemetry = new Mock<IBitbucketTelemetryService>();
        var retry = new RetryPolicyHelper(Options.Create(new BitbucketOptions { Workspace = "ws", PageLength = 25, Username = "user", AppPassword = "pass", RepoNameFilter = "", RepoNameList = [], BranchNameList = [], RepoSearchMode = BBDevPulse.Models.RepoSearchMode.FilterFromTheList, PrTimeFilterMode = BBDevPulse.Models.PrTimeFilterMode.CreatedOnOnly, MaxRetries = 0 }));
        return new BitbucketResponseReader(new BitbucketTransport(http, retry, telemetry.Object), new PaginatorHelper());
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}

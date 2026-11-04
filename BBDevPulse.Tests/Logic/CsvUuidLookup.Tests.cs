using System.Net;

using BBDevPulse.CsvUuidLookup;

namespace BBDevPulse.Tests.Logic;

public sealed class CsvUuidLookupTests
{
    [Fact]
    public void ResolvesOnlyUniqueNamesAndPreservesExistingUuids()
    {
        var csv = new[] { "Name;Grade;Department", "Alice;Senior;Core", "Bob;Middle;Core", "Unknown;;Other", "Saved;Lead;Core;{saved}" };
        var members = new[] { new WorkspaceMember("alice", "{A}"), new WorkspaceMember("Bob", "{B1}"), new WorkspaceMember("Bob", "{B2}") };
        var result = CsvResolver.Resolve(csv, members);
        Assert.Equal("Name;Grade;Department;UUID", result.Lines[0]);
        Assert.Equal("Alice;Senior;Core;{A}", result.Lines[1]);
        Assert.Equal("Bob;Middle;Core;", result.Lines[2]);
        Assert.Equal("Unknown;;Other;", result.Lines[3]);
        Assert.Equal("Saved;Lead;Core;{saved}", result.Lines[4]);
        Assert.Equal(1, result.Filled);
        Assert.Equal("3,4", string.Join(",", result.Issues.Select(issue => issue.Line)));
        Assert.Contains("{B1}", result.Issues[0].Message);
        Assert.Equal("Name;Grade;Department", csv[0]);
    }

    [Fact]
    public void DoesNotAssignSameUuidToMultipleCsvRows()
    {
        var result = CsvResolver.Resolve(["Alex;A;One", "Alex;B;Two"], [new WorkspaceMember("Alex", "{same}")]);
        Assert.Equal("Alex;A;One;|Alex;B;Two;", string.Join("|", result.Lines));
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal(0, result.Filled);
    }

    [Fact]
    public void ExistingUuidWinsAndConflictingProposedUuidIsNotAdded()
    {
        var result = CsvResolver.Resolve(["Alex;A;One;{same}", "Alias;B;Two"], [new WorkspaceMember("Alias", "SAME")]);
        Assert.Equal("Alex;A;One;{same}", result.Lines[0]);
        Assert.Equal("Alias;B;Two;", result.Lines[1]);
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public void MissingUuidOnMatchingMemberPreventsUnsafeAssignment()
    {
        var result = CsvResolver.Resolve(["Alex;A;One"], [new WorkspaceMember("Alex", "a"), new WorkspaceMember("Alex", null)]);
        Assert.Equal("Alex;A;One;", result.Lines[0]);
        Assert.Single(result.Issues);
    }

    [Theory]
    [InlineData("No;Name")]
    [InlineData(";Grade;Team")]
    [InlineData("A;B;C;D;E")]
    public void InvalidCsvIsRejected(string line) => Assert.Throws<InvalidDataException>(() => CsvResolver.Resolve([line], []));

    [Fact]
    public async Task ReadsAllWorkspaceMemberPages()
    {
        var handler = new StubHandler(request => request.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal)
            ? """{"values":[{"user":{"display_name":"Bob","uuid":"{b}"}}]}"""
            : """{"values":[{"user":{"display_name":"Alice","uuid":"{a}"}}],"next":"https://api.bitbucket.org/2.0/workspaces/ws/members?page=2"}""");
        using var client = new HttpClient(handler);
        var members = await new WorkspaceMembersClient(client).GetMembersAsync("ws", CancellationToken.None);
        Assert.Equal("Alice,Bob", string.Join(",", members.Select(member => member.Name)));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task RejectsForeignPaginationUrlBeforeSendingCredentials()
    {
        var handler = new StubHandler(_ => """{"values":[],"next":"https://evil.example/members"}""");
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new WorkspaceMembersClient(client).GetMembersAsync("ws", CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RejectsUnnamedMemberRatherThanAssumingUniqueName()
    {
        var handler = new StubHandler(_ => """{"values":[{"user":{"uuid":"{a}"}}]}""");
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new WorkspaceMembersClient(client).GetMembersAsync("ws", CancellationToken.None));
    }

    [Fact]
    public async Task RejectsRepeatedPaginationUrl()
    {
        var handler = new StubHandler(_ => """{"values":[],"next":"https://api.bitbucket.org/2.0/workspaces/ws/members?pagelen=100"}""");
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new WorkspaceMembersClient(client).GetMembersAsync("ws", CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void ReportsDuplicateExistingUuidsIgnoringBracesAndCase()
    {
        var result = CsvResolver.Resolve(["A;Senior;X;{ID}", "B;Middle;Y;id"], []);
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal("A;Senior;X;{ID}", result.Lines[0]);
        Assert.Equal("B;Middle;Y;id", result.Lines[1]);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(request)) });
        }
    }
}

using FluentAssertions;

using BBDevPulse.API;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Tests.API;

public sealed class RetryPolicyHelperTests
{
    [Fact]
    public async Task ExecuteAsyncPreservesStatusWhenRetryBudgetIsExhausted()
    {
        var options = Options.Create(new BitbucketOptions
        {
            Days = 7, Workspace = "workspace", PageLength = 25,
            MaxRetries = 0, Username = "user", AppPassword = "pass",
            RepoNameFilter = string.Empty, RepoNameList = [], BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly
        });
        var helper = new RetryPolicyHelper(options);
        var failure = new RetryableBitbucketException(System.Net.HttpStatusCode.ServiceUnavailable, "busy");

        Func<Task> act = () => helper.ExecuteAsync<int>(_ => Task.FromException<int>(failure),
            ex => ex is RetryableBitbucketException, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<RetryableBitbucketException>();
        thrown.Which.Should().BeSameAs(failure);
        thrown.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData("120")]
    [InlineData("Wed, 21 Oct 2099 07:28:00 GMT")]
    public async Task ExecuteAsyncRejectsRetryAfterBeyondMaximum(string retryAfter)
    {
        var options = Options.Create(new BitbucketOptions
        {
            Days = 7, Workspace = "workspace", PageLength = 25,
            MaxRetries = 1, MaxRetryDelaySeconds = 30,
            Username = "user", AppPassword = "pass", RepoNameFilter = string.Empty,
            RepoNameList = [], BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly
        });
        var helper = new RetryPolicyHelper(options);
        var attempts = 0;

        Func<Task> act = () => helper.ExecuteAsync<int>(_ =>
        {
            attempts++;
            throw new RetryableBitbucketException(System.Net.HttpStatusCode.TooManyRequests, "busy", retryAfter);
        }, ex => ex is RetryableBitbucketException, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("Retry-After");
        attempts.Should().Be(1);
    }

    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new RetryPolicyHelper(options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory(DisplayName = "GetDelay increases by configured step and is clamped to configured maximum")]
    [Trait("Category", "Unit")]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 5)]
    [InlineData(20, 30)]
    public void GetDelayWhenCalledReturnsExpectedDelay(int retryAttempt, int expectedDelaySeconds)
    {
        // Arrange
        var helper = new RetryPolicyHelper(Options.Create(new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 25,
            PullRequestConcurrency = 1,
            RepositoryConcurrency = 1,
            MaxRetries = 5,
            RetryDelayStepSeconds = 2,
            MaxRetryDelaySeconds = 30,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Pdf = new PdfOptions()
        }));

        // Act
        var delay = helper.GetDelay(retryAttempt);

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(expectedDelaySeconds));
    }

}

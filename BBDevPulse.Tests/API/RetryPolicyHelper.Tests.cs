using FluentAssertions;

using BBDevPulse.API;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;
using System.Globalization;

namespace BBDevPulse.Tests.API;

public sealed class RetryPolicyHelperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsyncUsesSharedCooldownForRetryAfter(bool useHttpDate)
    {
        var clock = new ManualTimeProvider();
        var helper = new RetryPolicyHelper(CreateRetryOptions(), clock);
        var header = useHttpDate
            ? clock.GetUtcNow().AddSeconds(5).ToString("r", CultureInfo.InvariantCulture)
            : "5";
        var calls = 0;
        var first = helper.ExecuteAsync(_ =>
        {
            calls++;
            return calls == 1
                ? Task.FromException<int>(new RetryableBitbucketException(System.Net.HttpStatusCode.TooManyRequests, "busy", header))
                : Task.FromResult(1);
        }, ex => ex is RetryableBitbucketException, CancellationToken.None);
        var second = helper.ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult(2);
        }, ex => ex is RetryableBitbucketException, CancellationToken.None);

        calls.Should().Be(1);
        clock.Advance(TimeSpan.FromSeconds(4));
        await Task.Yield();
        calls.Should().Be(1);
        clock.Advance(TimeSpan.FromSeconds(1));
        await Task.WhenAll(first, second);
        calls.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsyncFallsBackForMalformedRetryAfterAndCancelsWait()
    {
        var clock = new ManualTimeProvider();
        var helper = new RetryPolicyHelper(CreateRetryOptions(), clock);
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var run = helper.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromException<int>(new RetryableBitbucketException(
                System.Net.HttpStatusCode.TooManyRequests, "busy", "invalid"));
        }, ex => ex is RetryableBitbucketException, cancellation.Token);

        calls.Should().Be(1);
        clock.Advance(TimeSpan.FromMilliseconds(900));
        await Task.Yield();
        calls.Should().Be(1);
        cancellation.Cancel();
        await FluentActions.Invoking(async () => await run).Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(1);
    }

    private static IOptions<BitbucketOptions> CreateRetryOptions() => Options.Create(new BitbucketOptions
    {
        Days = 7, Workspace = "workspace", PageLength = 25,
        MaxRetries = 1, MaxRetryDelaySeconds = 30,
        Username = "user", AppPassword = "pass", RepoNameFilter = string.Empty,
        RepoNameList = [], BranchNameList = [],
        RepoSearchMode = RepoSearchMode.FilterFromTheList,
        PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly
    });

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        private readonly List<ManualTimer> _timers = [];

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state,
            TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan amount)
        {
            _now += amount;
            foreach (var timer in _timers.ToArray())
            {
                timer.FireIfDue(_now);
            }
        }

        private sealed class ManualTimer(ManualTimeProvider provider, TimerCallback callback, object? state) : ITimer
        {
            private DateTimeOffset _due;
            private bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _due = provider._now + dueTime;
                provider._timers.Add(this);
                return true;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                if (_disposed || now < _due)
                {
                    return;
                }

                _disposed = true;
                callback(state);
            }

            public void Dispose() => _disposed = true;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

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

using FluentAssertions;

using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreAuthPresenterTests
{
    [Fact(DisplayName = "AnnounceAuthAsync throws when fetch user callback is null")]
    [Trait("Category", "Unit")]
    public async Task AnnounceAuthAsyncWhenFetchUserIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SpectreAuthPresenter();
        Func<CancellationToken, Task<AuthUser>> fetchUser = null!;

        // Act
        Func<Task> act = async () => await presenter.AnnounceAuthAsync(fetchUser, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "AnnounceAuthAsync writes success line when authentication succeeds")]
    [Trait("Category", "Unit")]
    public async Task AnnounceAuthAsyncWhenAuthenticationSucceedsWritesSuccessMessage()
    {
        // Arrange
        var presenter = new SpectreAuthPresenter();
        var user = new AuthUser(
            new DisplayName("Alice"),
            new Username("alice"),
            new UserUuid("{uuid}"));

        // Act
        var output = await TestConsoleRunner.RunAsync(
            async _ => await presenter.AnnounceAuthAsync(_ => Task.FromResult(user), CancellationToken.None));

        // Assert
        output.Should().Contain("Authenticating with Bitbucket...");
        output.Should().Contain("Auth succeeded for user:");
        output.Should().Contain("Alice");
    }

    [Fact(DisplayName = "AnnounceAuthAsync writes failure line and rethrows when authentication fails")]
    [Trait("Category", "Unit")]
    public async Task AnnounceAuthAsyncWhenAuthenticationFailsWritesFailureMessageAndRethrows()
    {
        // Arrange
        var presenter = new SpectreAuthPresenter();
        const string errorMessage = "bad credentials";

        // Act
        Exception? capturedException = null;
        var output = await TestConsoleRunner.RunAsync(async _ =>
        {
            try
            {
                await presenter.AnnounceAuthAsync(
                    _ => Task.FromException<AuthUser>(new InvalidOperationException(errorMessage)),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        // Assert
        capturedException.Should().NotBeNull();
        capturedException.Should().BeOfType<InvalidOperationException>();
        capturedException!.Message.Should().Be(errorMessage);
        output.Should().Contain("Auth failed:");
        output.Should().Contain(errorMessage);
    }
}

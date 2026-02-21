using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PullRequestDestinationTests
{
    [Fact(DisplayName = "Constructor throws when branch is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenBranchIsNullThrowsArgumentNullException()
    {
        // Arrange
        PullRequestBranch branch = null!;

        // Act
        Action act = () => _ = new PullRequestDestination(branch);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor sets branch")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenBranchIsValidSetsBranch()
    {
        // Arrange
        var branch = new PullRequestBranch("develop");

        // Act
        var destination = new PullRequestDestination(branch);

        // Assert
        destination.Branch.Should().Be(branch);
    }
}

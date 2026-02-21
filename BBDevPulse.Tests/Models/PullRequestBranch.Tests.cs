using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PullRequestBranchTests
{
    [Fact(DisplayName = "Constructor throws when name is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenNameIsNullThrowsArgumentNullException()
    {
        // Arrange
        string name = null!;

        // Act
        Action act = () => _ = new PullRequestBranch(name);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor sets name")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenNameIsValidSetsName()
    {
        // Arrange
        var name = "develop";

        // Act
        var branch = new PullRequestBranch(name);

        // Assert
        branch.Name.Should().Be(name);
    }
}

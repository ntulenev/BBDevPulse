using FluentAssertions;

using BBDevPulse.Models;
using BBDevPulse.Presentation;
using BBDevPulse.Tests.TestInfrastructure;

namespace BBDevPulse.Tests.Presentation;

public sealed class SpectreBranchFilterPresenterTests
{
    [Fact(DisplayName = "RenderBranchFilterInfo throws when branch list is null")]
    [Trait("Category", "Unit")]
    public void RenderBranchFilterInfoWhenBranchListIsNullThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SpectreBranchFilterPresenter();
        IReadOnlyList<BranchName> branchList = null!;

        // Act
        Action act = () => presenter.RenderBranchFilterInfo(branchList);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderBranchFilterInfo writes nothing when branch list is empty")]
    [Trait("Category", "Unit")]
    public void RenderBranchFilterInfoWhenBranchListIsEmptyWritesNothing()
    {
        // Arrange
        var presenter = new SpectreBranchFilterPresenter();

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderBranchFilterInfo([]));

        // Assert
        output.Should().BeEmpty();
    }

    [Fact(DisplayName = "RenderBranchFilterInfo writes comma-separated branch list")]
    [Trait("Category", "Unit")]
    public void RenderBranchFilterInfoWhenBranchListHasValuesWritesJoinedBranchNames()
    {
        // Arrange
        var presenter = new SpectreBranchFilterPresenter();
        IReadOnlyList<BranchName> branchList = [new BranchName("develop"), new BranchName("master")];

        // Act
        var output = TestConsoleRunner.Run(_ => presenter.RenderBranchFilterInfo(branchList));

        // Assert
        output.Should().Contain("Filtering PRs by target branches:");
        output.Should().Contain("develop, master");
    }
}

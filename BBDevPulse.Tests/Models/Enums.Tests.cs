using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class EnumsTests
{
    [Fact(DisplayName = "PrTimeFilterMode values are stable")]
    [Trait("Category", "Unit")]
    public void PrTimeFilterModeValuesAreStable()
    {
        // Assert
        ((int)PrTimeFilterMode.LastKnownUpdateAndCreated).Should().Be(1);
        ((int)PrTimeFilterMode.CreatedOnOnly).Should().Be(2);
    }

    [Fact(DisplayName = "RepoSearchMode values are stable")]
    [Trait("Category", "Unit")]
    public void RepoSearchModeValuesAreStable()
    {
        // Assert
        ((int)RepoSearchMode.SearchByFilter).Should().Be(1);
        ((int)RepoSearchMode.FilterFromTheList).Should().Be(2);
    }

    [Fact(DisplayName = "PullRequestState values are stable")]
    [Trait("Category", "Unit")]
    public void PullRequestStateValuesAreStable()
    {
        // Assert
        ((int)PullRequestState.Unknown).Should().Be(0);
        ((int)PullRequestState.Open).Should().Be(1);
        ((int)PullRequestState.Merged).Should().Be(2);
        ((int)PullRequestState.Declined).Should().Be(3);
        ((int)PullRequestState.Superseded).Should().Be(4);
    }
}

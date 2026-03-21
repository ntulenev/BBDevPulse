using FluentAssertions;

using BBDevPulse.Abstractions;

namespace BBDevPulse.Tests.Abstractions;

public sealed class BitbucketFieldGroupTests
{
    [Fact(DisplayName = "BitbucketFieldGroup values are stable")]
    [Trait("Category", "Unit")]
    public void BitbucketFieldGroupValuesAreStable()
    {
        // Assert
        ((int)BitbucketFieldGroup.None).Should().Be(0);
        ((int)BitbucketFieldGroup.RepositoryList).Should().Be(1);
        ((int)BitbucketFieldGroup.PullRequestList).Should().Be(2);
        ((int)BitbucketFieldGroup.PullRequestActivity).Should().Be(3);
        ((int)BitbucketFieldGroup.PullRequestCommit).Should().Be(4);
        ((int)BitbucketFieldGroup.PullRequestSizeReference).Should().Be(5);
        ((int)BitbucketFieldGroup.PullRequestDiffStat).Should().Be(6);
    }
}

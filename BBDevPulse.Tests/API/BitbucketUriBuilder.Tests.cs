using FluentAssertions;

using BBDevPulse.API;
using BBDevPulse.Abstractions;

namespace BBDevPulse.Tests.API;

public sealed class BitbucketUriBuilderTests
{
    [Fact(DisplayName = "BuildRelativeUri throws when path is null")]
    [Trait("Category", "Unit")]
    public void BuildRelativeUriWhenPathIsNullThrowsArgumentException()
    {
        // Arrange
        var builder = new BitbucketUriBuilder();
        string path = null!;

        // Act
        Action act = () => _ = builder.BuildRelativeUri(path);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "BuildRelativeUri returns plain relative URI when field group is none")]
    [Trait("Category", "Unit")]
    public void BuildRelativeUriWhenFieldGroupIsNoneReturnsPathWithoutFields()
    {
        // Arrange
        var builder = new BitbucketUriBuilder();

        // Act
        var result = builder.BuildRelativeUri("repositories/ws?pagelen=25", BitbucketFieldGroup.None);

        // Assert
        result.ToString().Should().Be("repositories/ws?pagelen=25");
    }

    [Fact(DisplayName = "BuildRelativeUri appends encoded fields for repository list group")]
    [Trait("Category", "Unit")]
    public void BuildRelativeUriWhenRepositoryListGroupSelectedAppendsEncodedFields()
    {
        // Arrange
        var builder = new BitbucketUriBuilder();

        // Act
        var result = builder.BuildRelativeUri("repositories/ws?pagelen=25", BitbucketFieldGroup.RepositoryList);

        // Assert
        result.ToString().Should().Be(
            "repositories/ws?pagelen=25&fields=next%2Cvalues.name%2Cvalues.slug");
    }

    [Fact(DisplayName = "BuildRelativeUri appends fields with question mark when path has no query")]
    [Trait("Category", "Unit")]
    public void BuildRelativeUriWhenPathHasNoQueryUsesQuestionMarkSeparator()
    {
        // Arrange
        var builder = new BitbucketUriBuilder();

        // Act
        var result = builder.BuildRelativeUri(
            "repositories/ws/repo/pullrequests/7",
            BitbucketFieldGroup.PullRequestSizeReference);

        // Assert
        result.ToString().Should().Be(
            "repositories/ws/repo/pullrequests/7?fields=source.commit.hash%2Cdestination.commit.hash");
    }

    [Fact(DisplayName = "BuildRelativeUri ignores unknown field group values")]
    [Trait("Category", "Unit")]
    public void BuildRelativeUriWhenFieldGroupIsUnknownReturnsPathWithoutFields()
    {
        // Arrange
        var builder = new BitbucketUriBuilder();

        // Act
        var result = builder.BuildRelativeUri("repositories/ws?pagelen=25", (BitbucketFieldGroup)999);

        // Assert
        result.ToString().Should().Be("repositories/ws?pagelen=25");
    }
}

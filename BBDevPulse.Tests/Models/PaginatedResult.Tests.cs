using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class PaginatedResultTests
{
    [Fact(DisplayName = "Constructor sets items and next page")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenArgumentsAreValidSetsProperties()
    {
        // Arrange
        IReadOnlyList<int> items = [1, 2, 3];
        var nextPage = new Uri("https://example.test/page/2", UriKind.Absolute);

        // Act
        var result = new PaginatedResult<int>(items, nextPage);

        // Assert
        result.Items.Should().BeSameAs(items);
        result.NextPage.Should().Be(nextPage);
    }

    [Fact(DisplayName = "Record equality compares values")]
    [Trait("Category", "Unit")]
    public void EqualityWhenValuesAreSameReturnsTrue()
    {
        // Arrange
        var nextPage = new Uri("https://example.test/page/2", UriKind.Absolute);
        IReadOnlyList<int> items = [1, 2];
        var left = new PaginatedResult<int>(items, nextPage);
        var right = new PaginatedResult<int>(items, nextPage);

        // Act
        var areEqual = left == right;

        // Assert
        areEqual.Should().BeTrue();
    }
}

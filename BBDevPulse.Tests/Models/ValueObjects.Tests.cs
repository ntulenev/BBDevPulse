using FluentAssertions;

using BBDevPulse.Models;

namespace BBDevPulse.Tests.Models;

public sealed class ValueObjectsTests
{
    [Theory(DisplayName = "Constructor throws when value is null")]
    [InlineData("BranchName")]
    [InlineData("DisplayName")]
    [InlineData("RepoName")]
    [InlineData("RepoNameFilter")]
    [InlineData("RepoSlug")]
    [InlineData("Username")]
    [InlineData("UserUuid")]
    [InlineData("Workspace")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueIsNullThrowsArgumentNullException(string typeName)
    {
        // Arrange
        string value = null!;

        // Act
        Action act = () => _ = Create(typeName, value);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory(DisplayName = "Constructor throws when value is whitespace for required non-empty value objects")]
    [InlineData("BranchName")]
    [InlineData("DisplayName")]
    [InlineData("RepoName")]
    [InlineData("RepoSlug")]
    [InlineData("Username")]
    [InlineData("UserUuid")]
    [InlineData("Workspace")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueIsWhitespaceThrowsArgumentException(string typeName)
    {
        // Arrange
        var value = "   ";

        // Act
        Action act = () => _ = Create(typeName, value);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Constructor sets value and ToString returns value")]
    [InlineData("BranchName", "develop")]
    [InlineData("DisplayName", "Alice Doe")]
    [InlineData("RepoName", "Repo-One")]
    [InlineData("RepoNameFilter", "abc")]
    [InlineData("RepoSlug", "repo-one")]
    [InlineData("Username", "alice")]
    [InlineData("UserUuid", "{uuid}")]
    [InlineData("Workspace", "workspace")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueIsValidSetsValueAndToString(string typeName, string value)
    {
        // Arrange
        var created = Create(typeName, value);

        // Act
        var resolvedValue = ReadValue(created);
        var text = created.ToString();

        // Assert
        resolvedValue.Should().Be(value);
        text.Should().Be(value);
    }

    private static object Create(string typeName, string value)
    {
        return typeName switch
        {
            "BranchName" => new BranchName(value),
            "DisplayName" => new DisplayName(value),
            "RepoName" => new RepoName(value),
            "RepoNameFilter" => new RepoNameFilter(value),
            "RepoSlug" => new RepoSlug(value),
            "Username" => new Username(value),
            "UserUuid" => new UserUuid(value),
            "Workspace" => new Workspace(value),
            _ => throw new NotSupportedException(typeName)
        };
    }

    private static string ReadValue(object valueObject)
    {
        return valueObject switch
        {
            BranchName item => item.Value,
            DisplayName item => item.Value,
            RepoName item => item.Value,
            RepoNameFilter item => item.Value,
            RepoSlug item => item.Value,
            Username item => item.Value,
            UserUuid item => item.Value,
            Workspace item => item.Value,
            _ => throw new NotSupportedException(valueObject.GetType().Name)
        };
    }
}

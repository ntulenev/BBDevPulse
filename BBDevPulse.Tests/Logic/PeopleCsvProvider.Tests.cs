using FluentAssertions;

using BBDevPulse.Configuration;
using BBDevPulse.Logic;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Tests.Logic;

public sealed class PeopleCsvProviderTests
{
    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BitbucketOptions> options = null!;

        // Act
        Action act = () => _ = new PeopleCsvProvider(options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "GetPeopleByDisplayName returns empty dictionary when PeopleCsvPath is not configured")]
    [Trait("Category", "Unit")]
    public async Task GetPeopleByDisplayNameAsyncWhenPathIsNotConfiguredReturnsEmptyDictionary()
    {
        // Arrange
        var sut = new PeopleCsvProvider(Options.Create(CreateOptions(peopleCsvPath: null)));

        // Act
        var result = await sut.GetPeopleByDisplayNameAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "GetPeopleByDisplayName throws when configured CSV file does not exist")]
    [Trait("Category", "Unit")]
    public async Task GetPeopleByDisplayNameAsyncWhenFileDoesNotExistThrowsFileNotFoundException()
    {
        // Arrange
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "people.csv");
        var sut = new PeopleCsvProvider(Options.Create(CreateOptions(peopleCsvPath: missingPath)));

        // Act
        Func<Task> act = () => sut.GetPeopleByDisplayNameAsync();

        // Assert
        (await act.Should().ThrowAsync<FileNotFoundException>())
            .WithMessage($"People CSV file '{missingPath}' was not found.");
    }

    [Fact(DisplayName = "GetPeopleByDisplayName parses CSV rows and matches keys by display name value")]
    [Trait("Category", "Unit")]
    public async Task GetPeopleByDisplayNameAsyncWhenCsvIsValidParsesRowsAndSupportsDisplayNameKeyLookup()
    {
        // Arrange
        var csvPath = Path.GetTempFileName();
        File.WriteAllLines(
            csvPath,
            [
                "Name;Grade;Department",
                "Alice;Senior;Platform",
                "Bob;;",
                "Alice;Lead;Core"
            ]);

        try
        {
            var sut = new PeopleCsvProvider(Options.Create(CreateOptions(peopleCsvPath: csvPath)));

            // Act
            var result = await sut.GetPeopleByDisplayNameAsync();

            // Assert
            result.Should().HaveCount(2);
            result.TryGetValue(new DisplayName("Alice"), out var alice).Should().BeTrue();
            alice.Grade.Should().Be("Lead");
            alice.Department.Should().Be("Core");
            result.TryGetValue(new DisplayName("Bob"), out var bob).Should().BeTrue();
            bob.Grade.Should().Be(DeveloperStats.NOT_AVAILABLE);
            bob.Department.Should().Be(DeveloperStats.NOT_AVAILABLE);
        }
        finally
        {
            if (File.Exists(csvPath))
            {
                File.Delete(csvPath);
            }
        }
    }

    [Fact(DisplayName = "GetPeopleByDisplayName throws when CSV has invalid structure")]
    [Trait("Category", "Unit")]
    public async Task GetPeopleByDisplayNameAsyncWhenCsvStructureInvalidThrowsFormatException()
    {
        // Arrange
        var csvPath = Path.GetTempFileName();
        File.WriteAllLines(
            csvPath,
            [
                "Name;Grade;Department",
                "Alice;Senior"
            ]);

        try
        {
            var sut = new PeopleCsvProvider(Options.Create(CreateOptions(peopleCsvPath: csvPath)));

            // Act
            Func<Task> act = () => sut.GetPeopleByDisplayNameAsync();

            // Assert
            (await act.Should().ThrowAsync<FormatException>())
                .WithMessage("Invalid people CSV format at line 2. Expected 'Name;Grade;Department'.");
        }
        finally
        {
            if (File.Exists(csvPath))
            {
                File.Delete(csvPath);
            }
        }
    }

    private static BitbucketOptions CreateOptions(string? peopleCsvPath)
    {
        return new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 25,
            PullRequestConcurrency = 1,
            RepositoryConcurrency = 1,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            PeopleCsvPath = peopleCsvPath,
            Pdf = new PdfOptions()
        };
    }
}

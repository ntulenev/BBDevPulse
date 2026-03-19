using FluentAssertions;

using BBDevPulse.Configuration;
using BBDevPulse.Models;

namespace BBDevPulse.Tests.Configuration;

public sealed class BitbucketOptionsValidatorTests
{
    [Fact(DisplayName = "Validate throws when options are null")]
    [Trait("Category", "Unit")]
    public void ValidateWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        var validator = new BitbucketOptionsValidator();
        BitbucketOptions options = null!;

        // Act
        Action act = () => _ = validator.Validate(name: null, options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Validate returns success for valid options")]
    [Trait("Category", "Unit")]
    public void ValidateWhenOptionsAreValidReturnsSuccess()
    {
        // Arrange
        var validator = new BitbucketOptionsValidator();
        var options = CreateValidOptions();

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Failed.Should().BeFalse();
    }

    [Fact(DisplayName = "Validate returns all expected failures for invalid options")]
    [Trait("Category", "Unit")]
    public void ValidateWhenOptionsAreInvalidReturnsAllValidationErrors()
    {
        // Arrange
        var validator = new BitbucketOptionsValidator();
        var options = new BitbucketOptions
        {
            Days = 0,
            Workspace = " ",
            PageLength = 0,
            PullRequestConcurrency = 0,
            RepositoryConcurrency = 0,
            MaxRetries = -1,
            RetryDelayStepSeconds = 0,
            MaxRetryDelaySeconds = 0,
            Username = "",
            AppPassword = " ",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Pdf = new PdfOptions()
        };

        // Act
        var result = validator.Validate(name: "Bitbucket", options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Bitbucket:Days must be greater than 0 when Bitbucket:FromDate and Bitbucket:ToDate are not configured.");
        result.Failures.Should().Contain("Bitbucket:PageLength must be greater than 0.");
        result.Failures.Should().Contain("Bitbucket:PullRequestConcurrency must be greater than 0.");
        result.Failures.Should().Contain("Bitbucket:RepositoryConcurrency must be greater than 0.");
        result.Failures.Should().Contain("Bitbucket:MaxRetries must be greater than or equal to 0.");
        result.Failures.Should().Contain("Bitbucket:RetryDelayStepSeconds must be greater than 0.");
        result.Failures.Should().Contain("Bitbucket:MaxRetryDelaySeconds must be greater than 0.");
        result.Failures.Should().Contain("Bitbucket:Workspace is required.");
        result.Failures.Should().Contain("Bitbucket:Username is required.");
        result.Failures.Should().Contain("Bitbucket:AppPassword is required.");
    }

    [Fact(DisplayName = "Validate requires FromDate and ToDate together")]
    [Trait("Category", "Unit")]
    public void ValidateWhenOnlyOneDateRangeBoundConfiguredReturnsFailure()
    {
        // Arrange
        var validator = new BitbucketOptionsValidator();
        var options = new BitbucketOptions
        {
            Days = null,
            FromDate = "2026-02-01",
            Workspace = "workspace",
            PageLength = 25,
            PullRequestConcurrency = 2,
            RepositoryConcurrency = 2,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            PeopleCsvPath = "people.csv",
            Pdf = new PdfOptions()
        };

        // Act
        var result = validator.Validate(name: "Bitbucket", options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Bitbucket:FromDate and Bitbucket:ToDate must be configured together.");
    }

    [Fact(DisplayName = "Validate requires people CSV path when team filter is configured")]
    [Trait("Category", "Unit")]
    public void ValidateWhenTeamFilterConfiguredWithoutPeopleCsvPathReturnsFailure()
    {
        // Arrange
        var validator = new BitbucketOptionsValidator();
        var options = new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 25,
            PullRequestConcurrency = 2,
            RepositoryConcurrency = 2,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            TeamFilter = "Core",
            Pdf = new PdfOptions()
        };

        // Act
        var result = validator.Validate(name: "Bitbucket", options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Bitbucket:PeopleCsvPath is required when Bitbucket:TeamFilter is configured.");
    }

    private static BitbucketOptions CreateValidOptions()
    {
        return new BitbucketOptions
        {
            Days = 7,
            Workspace = "workspace",
            PageLength = 25,
            PullRequestConcurrency = 2,
            RepositoryConcurrency = 2,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = [],
            BranchNameList = [],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            PeopleCsvPath = "people.csv",
            Pdf = new PdfOptions()
        };
    }
}

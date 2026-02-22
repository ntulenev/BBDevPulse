using FluentAssertions;

using BBDevPulse.Configuration;
using BBDevPulse.Models;

namespace BBDevPulse.Tests.Configuration;

public sealed class BitbucketOptionsTests
{
    [Fact(DisplayName = "CreateReportParameters maps options and filters blank list entries")]
    [Trait("Category", "Unit")]
    public void CreateReportParametersWhenOptionsAreValidMapsValuesAndFiltersWhitespaceEntries()
    {
        // Arrange
        var options = new BitbucketOptions
        {
            Days = 10,
            Workspace = "workspace",
            PageLength = 25,
            PullRequestConcurrency = 3,
            RepositoryConcurrency = 2,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = "pulse",
            RepoNameList = ["RepoA", " ", "", "RepoB"],
            BranchNameList = ["main", "", "develop", " "],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.LastKnownUpdateAndCreated,
            ExcludeWeekend = true,
            ExcludedDays = ["03.02.2026", "2026-02-04", " ", "", "03.02.2026"],
            Pdf = new PdfOptions { Enabled = false, OutputPath = "report.pdf" }
        };
        var expectedLowerBound = DateTimeOffset.UtcNow.AddDays(-10).AddSeconds(-2);
        var expectedUpperBound = DateTimeOffset.UtcNow.AddDays(-10).AddSeconds(2);

        // Act
        var parameters = options.CreateReportParameters();

        // Assert
        parameters.FilterDate.Should().BeOnOrAfter(expectedLowerBound);
        parameters.FilterDate.Should().BeOnOrBefore(expectedUpperBound);
        parameters.Workspace.Value.Should().Be("workspace");
        parameters.RepoNameFilter.Value.Should().Be("pulse");
        parameters.RepoNameList.Select(name => name.Value).Should().Equal("RepoA", "RepoB");
        parameters.BranchNameList.Select(name => name.Value).Should().Equal("main", "develop");
        parameters.RepoSearchMode.Should().Be(RepoSearchMode.FilterFromTheList);
        parameters.PrTimeFilterMode.Should().Be(PrTimeFilterMode.LastKnownUpdateAndCreated);
        parameters.ExcludeWeekend.Should().BeTrue();
        parameters.ExcludedDays.Should().Contain(new DateOnly(2026, 2, 3));
        parameters.ExcludedDays.Should().Contain(new DateOnly(2026, 2, 4));
        parameters.ExcludedDays.Should().HaveCount(2);
    }

    [Fact(DisplayName = "CreateReportParameters treats null repo and branch lists as empty")]
    [Trait("Category", "Unit")]
    public void CreateReportParametersWhenListsAreNullTreatsThemAsEmpty()
    {
        // Arrange
        var options = new BitbucketOptions
        {
            Days = 1,
            Workspace = "workspace",
            PageLength = 10,
            PullRequestConcurrency = 2,
            RepositoryConcurrency = 2,
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = string.Empty,
            RepoNameList = null!,
            BranchNameList = null!,
            RepoSearchMode = RepoSearchMode.SearchByFilter,
            PrTimeFilterMode = PrTimeFilterMode.CreatedOnOnly,
            Pdf = new PdfOptions()
        };

        // Act
        var parameters = options.CreateReportParameters();

        // Assert
        parameters.RepoNameList.Should().BeEmpty();
        parameters.BranchNameList.Should().BeEmpty();
    }

    [Fact(DisplayName = "Bitbucket options expose configured property values")]
    [Trait("Category", "Unit")]
    public void PropertiesWhenInitializedReturnConfiguredValues()
    {
        // Arrange
        var pdf = new PdfOptions
        {
            Enabled = false,
            OutputPath = "report.pdf"
        };
        var options = new BitbucketOptions
        {
            Days = 5,
            Workspace = "workspace",
            PageLength = 50,
            PullRequestConcurrency = 5,
            RepositoryConcurrency = 4,
            Username = "username",
            AppPassword = "password",
            RepoNameFilter = "filter",
            RepoNameList = ["RepoA"],
            BranchNameList = ["main"],
            RepoSearchMode = RepoSearchMode.SearchByFilter,
            PrTimeFilterMode = PrTimeFilterMode.LastKnownUpdateAndCreated,
            ExcludeWeekend = true,
            ExcludedDays = ["03.02.2026"],
            PeopleCsvPath = "people.csv",
            Pdf = pdf
        };

        // Act
        var days = options.Days;
        var workspace = options.Workspace;
        var pageLength = options.PageLength;
        var pullRequestConcurrency = options.PullRequestConcurrency;
        var repositoryConcurrency = options.RepositoryConcurrency;
        var username = options.Username;
        var appPassword = options.AppPassword;
        var repoNameFilter = options.RepoNameFilter;
        var repoNameList = options.RepoNameList;
        var branchNameList = options.BranchNameList;
        var repoSearchMode = options.RepoSearchMode;
        var prTimeFilterMode = options.PrTimeFilterMode;
        var excludeWeekend = options.ExcludeWeekend;
        var excludedDays = options.ExcludedDays;
        var peopleCsvPath = options.PeopleCsvPath;
        var pdfOptions = options.Pdf;

        // Assert
        days.Should().Be(5);
        workspace.Should().Be("workspace");
        pageLength.Should().Be(50);
        pullRequestConcurrency.Should().Be(5);
        repositoryConcurrency.Should().Be(4);
        username.Should().Be("username");
        appPassword.Should().Be("password");
        repoNameFilter.Should().Be("filter");
        repoNameList.Should().Equal("RepoA");
        branchNameList.Should().Equal("main");
        repoSearchMode.Should().Be(RepoSearchMode.SearchByFilter);
        prTimeFilterMode.Should().Be(PrTimeFilterMode.LastKnownUpdateAndCreated);
        excludeWeekend.Should().BeTrue();
        excludedDays.Should().Equal("03.02.2026");
        peopleCsvPath.Should().Be("people.csv");
        pdfOptions.Should().BeSameAs(pdf);
    }

    [Fact(DisplayName = "CreateReportParameters throws when excluded day format is invalid")]
    [Trait("Category", "Unit")]
    public void CreateReportParametersWhenExcludedDayFormatInvalidThrowsFormatException()
    {
        // Arrange
        var options = new BitbucketOptions
        {
            Days = 5,
            Workspace = "workspace",
            PageLength = 50,
            PullRequestConcurrency = 1,
            RepositoryConcurrency = 1,
            Username = "username",
            AppPassword = "password",
            RepoNameFilter = "filter",
            RepoNameList = ["RepoA"],
            BranchNameList = ["main"],
            RepoSearchMode = RepoSearchMode.SearchByFilter,
            PrTimeFilterMode = PrTimeFilterMode.LastKnownUpdateAndCreated,
            ExcludedDays = ["02/03/2026"],
            Pdf = new PdfOptions()
        };

        // Act
        Action act = () => _ = options.CreateReportParameters();

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("Invalid excluded day '02/03/2026'. Expected dd.MM.yyyy or yyyy-MM-dd.");
    }
}

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
            Username = "user",
            AppPassword = "pass",
            RepoNameFilter = "pulse",
            RepoNameList = ["RepoA", " ", "", "RepoB"],
            BranchNameList = ["main", "", "develop", " "],
            RepoSearchMode = RepoSearchMode.FilterFromTheList,
            PrTimeFilterMode = PrTimeFilterMode.LastKnownUpdateAndCreated,
            ExcludeWeekend = true,
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
            Username = "username",
            AppPassword = "password",
            RepoNameFilter = "filter",
            RepoNameList = ["RepoA"],
            BranchNameList = ["main"],
            RepoSearchMode = RepoSearchMode.SearchByFilter,
            PrTimeFilterMode = PrTimeFilterMode.LastKnownUpdateAndCreated,
            ExcludeWeekend = true,
            Pdf = pdf
        };

        // Act
        var days = options.Days;
        var workspace = options.Workspace;
        var pageLength = options.PageLength;
        var username = options.Username;
        var appPassword = options.AppPassword;
        var repoNameFilter = options.RepoNameFilter;
        var repoNameList = options.RepoNameList;
        var branchNameList = options.BranchNameList;
        var repoSearchMode = options.RepoSearchMode;
        var prTimeFilterMode = options.PrTimeFilterMode;
        var excludeWeekend = options.ExcludeWeekend;
        var pdfOptions = options.Pdf;

        // Assert
        days.Should().Be(5);
        workspace.Should().Be("workspace");
        pageLength.Should().Be(50);
        username.Should().Be("username");
        appPassword.Should().Be("password");
        repoNameFilter.Should().Be("filter");
        repoNameList.Should().Equal("RepoA");
        branchNameList.Should().Equal("main");
        repoSearchMode.Should().Be(RepoSearchMode.SearchByFilter);
        prTimeFilterMode.Should().Be(PrTimeFilterMode.LastKnownUpdateAndCreated);
        excludeWeekend.Should().BeTrue();
        pdfOptions.Should().BeSameAs(pdf);
    }
}

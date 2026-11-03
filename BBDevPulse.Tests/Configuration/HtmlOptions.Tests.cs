using FluentAssertions;

using BBDevPulse.Configuration;

namespace BBDevPulse.Tests.Configuration;

public sealed class HtmlOptionsTests
{
    [Fact(DisplayName = "ResolveOutputPath prefixes a safe team name")]
    public void ResolveOutputPathWhenTeamSelectedPrefixesSlug()
    {
        var options = new HtmlOptions { OutputPath = "reports/bbdevpulse-report.html" };

        var outputPath = options.ResolveOutputPath("Core / Platform Team");

        Path.GetFileName(outputPath).Should().MatchRegex(@"^core-platform-team-bbdevpulse-report_\d{2}_\d{2}_\d{4}\.html$");
    }

    [Fact(DisplayName = "ResolveOutputPath uses default file name when output path is blank")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathWhenOutputPathIsBlankUsesDefaultNameWithDateSuffix()
    {
        // Arrange
        var options = new HtmlOptions
        {
            OutputPath = "   "
        };

        // Act
        var outputPath = options.ResolveOutputPath();

        // Assert
        Path.IsPathRooted(outputPath).Should().BeTrue();
        Path.GetFileName(outputPath).Should().MatchRegex(@"^bbdevpulse-report_\d{2}_\d{2}_\d{4}\.html$");
    }

    [Fact(DisplayName = "ResolveOutputPath preserves extension and trims relative path")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathWhenRelativePathHasExtensionPreservesExtension()
    {
        // Arrange
        var options = new HtmlOptions
        {
            OutputPath = " reports\\summary.txt "
        };

        // Act
        var outputPath = options.ResolveOutputPath();

        // Assert
        Path.GetFileName(outputPath).Should().MatchRegex(@"^summary_\d{2}_\d{2}_\d{4}\.txt$");
    }

    [Fact(DisplayName = "ResolveOutputPath appends HTML extension when output path has no extension")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathWhenPathHasNoExtensionAppendsHtmlExtension()
    {
        // Arrange
        var options = new HtmlOptions
        {
            OutputPath = "reports\\summary"
        };

        // Act
        var outputPath = options.ResolveOutputPath();

        // Assert
        Path.GetFileName(outputPath).Should().MatchRegex(@"^summary_\d{2}_\d{2}_\d{4}\.html$");
    }

    [Fact(DisplayName = "Enabled is true by default")]
    [Trait("Category", "Unit")]
    public void EnabledWhenNotConfiguredDefaultsToTrue()
    {
        // Arrange
        var options = new HtmlOptions();

        // Act
        var result = options.Enabled;

        // Assert
        result.Should().BeTrue();
    }
}

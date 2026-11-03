using FluentAssertions;

using BBDevPulse.Configuration;

namespace BBDevPulse.Tests.Configuration;

public sealed class PdfOptionsTests
{
    [Fact(DisplayName = "ResolveOutputPath prefixes a safe team name")]
    public void ResolveOutputPathWhenTeamSelectedPrefixesSlug()
    {
        var options = new PdfOptions { OutputPath = "reports/bbdevpulse-report.pdf" };

        var outputPath = options.ResolveOutputPath("Мой Отдел / API");

        Path.GetFileName(outputPath).Should().MatchRegex(@"^мой-отдел-api-bbdevpulse-report_\d{2}_\d{2}_\d{4}\.pdf$");
    }

    [Fact(DisplayName = "ResolveOutputPath uses default file name when output path is blank")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathWhenOutputPathIsBlankUsesDefaultNameWithDateSuffix()
    {
        // Arrange
        var options = new PdfOptions
        {
            OutputPath = "   "
        };

        // Act
        var outputPath = options.ResolveOutputPath();

        // Assert
        Path.IsPathRooted(outputPath).Should().BeTrue();
        Path.GetFileName(outputPath).Should().MatchRegex(@"^bbdevpulse-report_\d{2}_\d{2}_\d{4}\.pdf$");
    }

    [Fact(DisplayName = "ResolveOutputPath preserves the directory and extension and trims whitespace")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathWhenRelativePathHasExtensionPreservesExtension()
    {
        // Arrange
        var options = new PdfOptions
        {
            OutputPath = $" {Path.Combine("reports", "summary.txt")} "
        };

        // Act
        var outputPath = options.ResolveOutputPath();

        // Assert
        Path.GetDirectoryName(outputPath).Should().Be(Path.GetFullPath("reports"));
        Path.GetFileName(outputPath).Should().MatchRegex(@"^summary_\d{2}_\d{2}_\d{4}\.txt$");
    }

    [Fact(DisplayName = "ResolveOutputPath appends PDF extension when output path has no extension")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathWhenPathHasNoExtensionAppendsPdfExtension()
    {
        // Arrange
        var options = new PdfOptions
        {
            OutputPath = Path.Combine("reports", "summary")
        };

        // Act
        var outputPath = options.ResolveOutputPath();

        // Assert
        Path.GetDirectoryName(outputPath).Should().Be(Path.GetFullPath("reports"));
        Path.GetFileName(outputPath).Should().MatchRegex(@"^summary_\d{2}_\d{2}_\d{4}\.pdf$");
    }

    [Fact(DisplayName = "Enabled is true by default")]
    [Trait("Category", "Unit")]
    public void EnabledWhenNotConfiguredDefaultsToTrue()
    {
        // Arrange
        var options = new PdfOptions();

        // Act
        var result = options.Enabled;

        // Assert
        result.Should().BeTrue();
    }

}

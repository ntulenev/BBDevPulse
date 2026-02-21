using System.Reflection;

using FluentAssertions;

using BBDevPulse.Configuration;

namespace BBDevPulse.Tests.Configuration;

public sealed class PdfOptionsTests
{
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

    [Fact(DisplayName = "ResolveOutputPath preserves extension and trims relative path")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathWhenRelativePathHasExtensionPreservesExtension()
    {
        // Arrange
        var options = new PdfOptions
        {
            OutputPath = " reports\\summary.txt "
        };

        // Act
        var outputPath = options.ResolveOutputPath();

        // Assert
        Path.GetFileName(outputPath).Should().MatchRegex(@"^summary_\d{2}_\d{2}_\d{4}\.txt$");
    }

    [Fact(DisplayName = "ResolveOutputPath appends PDF extension when output path has no extension")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathWhenPathHasNoExtensionAppendsPdfExtension()
    {
        // Arrange
        var options = new PdfOptions
        {
            OutputPath = "reports\\summary"
        };

        // Act
        var outputPath = options.ResolveOutputPath();

        // Assert
        Path.GetFileName(outputPath).Should().MatchRegex(@"^summary_\d{2}_\d{2}_\d{4}\.pdf$");
    }

    [Fact(DisplayName = "AppendDateSuffix returns file name only when directory is missing")]
    [Trait("Category", "Unit")]
    public void AppendDateSuffixWhenDirectoryIsMissingReturnsDatedFileNameOnly()
    {
        // Arrange
        var date = new DateTime(2026, 2, 21);

        // Act
        var result = InvokeAppendDateSuffix("report.pdf", date);

        // Assert
        result.Should().Be("report_21_02_2026.pdf");
    }

    [Fact(DisplayName = "AppendDateSuffix throws when absolute path is blank")]
    [Trait("Category", "Unit")]
    public void AppendDateSuffixWhenPathIsBlankThrowsArgumentException()
    {
        // Arrange
        var method = typeof(PdfOptions).GetMethod(
            "AppendDateSuffix",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        // Act
        Action act = () => _ = method.Invoke(null, [" ", new DateTime(2026, 2, 21)]);

        // Assert
        var exception = act.Should().Throw<TargetInvocationException>().Which;
        exception.InnerException.Should().BeOfType<ArgumentException>();
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

    private static string InvokeAppendDateSuffix(string path, DateTime date)
    {
        var method = typeof(PdfOptions).GetMethod(
            "AppendDateSuffix",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [path, date])!;
    }
}

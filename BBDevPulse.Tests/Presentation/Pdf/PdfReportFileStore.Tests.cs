using FluentAssertions;

using BBDevPulse.Presentation.Pdf;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BBDevPulse.Tests.Presentation.Pdf;

public sealed class PdfReportFileStoreTests
{
    [Fact(DisplayName = "Save throws when output path is null")]
    [Trait("Category", "Unit")]
    public void SaveWhenOutputPathIsNullThrowsArgumentException()
    {
        // Arrange
        var store = new PdfReportFileStore();
        string outputPath = null!;
        var document = CreateDocument();

        // Act
        Action act = () => store.Save(outputPath, document);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Save throws when output path is whitespace")]
    [Trait("Category", "Unit")]
    public void SaveWhenOutputPathIsWhitespaceThrowsArgumentException()
    {
        // Arrange
        var store = new PdfReportFileStore();
        var document = CreateDocument();

        // Act
        Action act = () => store.Save("   ", document);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Save throws when document is null")]
    [Trait("Category", "Unit")]
    public void SaveWhenDocumentIsNullThrowsArgumentNullException()
    {
        // Arrange
        var store = new PdfReportFileStore();
        IDocument document = null!;

        // Act
        Action act = () => store.Save("report.pdf", document);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Save creates target directory and writes PDF file")]
    [Trait("Category", "Integration")]
    public void SaveWhenArgumentsAreValidCreatesDirectoryAndWritesFile()
    {
        // Arrange
        QuestPDF.Settings.License = LicenseType.Community;
        var store = new PdfReportFileStore();
        var tempDirectory = Path.Combine(Path.GetTempPath(), "BBDevPulse.Tests", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempDirectory, "nested", "report.pdf");
        var document = CreateDocument();

        try
        {
            // Act
            store.Save(outputPath, document);

            // Assert
            File.Exists(outputPath).Should().BeTrue();
            new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static Document CreateDocument()
    {
        return Document.Create(container =>
        {
            _ = container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().Text("BBDevPulse test pdf");
            });
        });
    }
}

using System.Text;

using BBDevPulse.Presentation.Html;

using FluentAssertions;

namespace BBDevPulse.Tests.Presentation.Html;

public sealed class HtmlReportFileStoreTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsyncRejectsMissingOutputPath(string? path)
    {
        var store = new HtmlReportFileStore();

        Func<Task> act = () => store.SaveAsync(path!, "report");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsyncRejectsNullHtml()
    {
        var store = new HtmlReportFileStore();

        Func<Task> act = () => store.SaveAsync("report.html", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsyncCreatesDirectoriesAndOverwritesWithUtf8WithoutBom()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BBDevPulse.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "nested", "report.html");
        var store = new HtmlReportFileStore();

        try
        {
            await store.SaveAsync(path, "an older and longer report");
            await store.SaveAsync(path, "<p>Hello 🌍</p>");

            var bytes = await File.ReadAllBytesAsync(path);
            bytes.Should().Equal(new UTF8Encoding(false).GetBytes("<p>Hello 🌍</p>"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAsyncHonorsCancellation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BBDevPulse.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "report.html");
        var store = new HtmlReportFileStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            Func<Task> act = () => store.SaveAsync(path, "report", cancellation.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

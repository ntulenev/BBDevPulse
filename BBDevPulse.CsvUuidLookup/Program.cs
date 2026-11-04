using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using BBDevPulse.CsvUuidLookup;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
    {
        Console.WriteLine("Usage: dotnet run --project BBDevPulse.CsvUuidLookup -- <input.csv> [--output <file.csv>] [--config <appsettings.json>]");
        Console.WriteLine("Set BBDEVPULSE_WORKSPACE, BBDEVPULSE_USERNAME and BBDEVPULSE_TOKEN, or use --config with Bitbucket settings.");
        return args.Length == 0 ? 1 : 0;
    }
    try
    {
        var input = args[0];
        string? output = null;
        string? configPath = null;
        for (var i = 1; i < args.Length; i++)
        {
            if ((args[i] != "--output" && args[i] != "--config") || i + 1 >= args.Length)
                throw new ArgumentException($"Unknown or incomplete option: {args[i]}");
            var option = args[i++];
            if (option == "--output")
                output = args[i];
            else
                configPath = args[i];
        }
        var inputPath = Path.GetFullPath(input);
        var outputPath = Path.GetFullPath(output ?? Path.Combine(Path.GetDirectoryName(inputPath)!,
            Path.GetFileNameWithoutExtension(inputPath) + ".with-uuids.csv"));
        if (string.Equals(inputPath, outputPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Output must differ from input; the input file is never overwritten.");
        if (File.Exists(outputPath))
            throw new IOException("Output file already exists; choose a different --output path.");
        var lines = await File.ReadAllLinesAsync(inputPath).ConfigureAwait(false);
        // Validate structure before making any network request.
        _ = CsvResolver.Resolve(lines, []);
        string? workspace = null;
        string? username = null;
        string? token = null;
        if (configPath is not null)
        {
            using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath).ConfigureAwait(false),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var bitbucket = config.RootElement.GetProperty("Bitbucket");
            workspace = GetString(bitbucket, "Workspace");
            username = GetString(bitbucket, "Username");
            token = GetString(bitbucket, "AppPassword");
        }
        workspace = Environment.GetEnvironmentVariable("BBDEVPULSE_WORKSPACE") ?? workspace;
        username = Environment.GetEnvironmentVariable("BBDEVPULSE_USERNAME") ?? username;
        token = Environment.GetEnvironmentVariable("BBDEVPULSE_TOKEN") ?? token;
        if (string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Provide workspace, username and token via BBDEVPULSE_WORKSPACE, BBDEVPULSE_USERNAME and BBDEVPULSE_TOKEN, or --config.");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{token}")));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        var members = await new WorkspaceMembersClient(http).GetMembersAsync(workspace.Trim(), cancellation.Token).ConfigureAwait(false);
        var result = CsvResolver.Resolve(lines, members);
        cancellation.Token.ThrowIfCancellationRequested();
        var temporaryPath = Path.Combine(Path.GetDirectoryName(outputPath)!, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var outputStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(outputStream, new UTF8Encoding(false)))
            {
                foreach (var line in result.Lines)
                    await writer.WriteLineAsync(line.AsMemory(), cancellation.Token).ConfigureAwait(false);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            File.Move(temporaryPath, outputPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        Console.WriteLine($"Workspace members read: {members.Count}. UUIDs filled: {result.Filled}.");
        foreach (var issue in result.Issues)
            Console.Error.WriteLine($"Line {issue.Line} ({issue.Name}): {issue.Message}.");
        Console.WriteLine($"Output: {outputPath}");
        if (result.Issues.Count > 0)
        {
            Console.Error.WriteLine("Resolve the listed rows manually before using this CSV in BBDevPulse.");
            return 2;
        }
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Cancelled.");
        return 1;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or JsonException or HttpRequestException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException)
    {
        Console.Error.WriteLine($"Error: {exception.Message}");
        return 1;
    }
}

static string? GetString(JsonElement element, string name) =>
    element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

using BBDevPulse.Abstractions;
using BBDevPulse.Configuration;
using BBDevPulse.Models;

using Microsoft.Extensions.Options;

namespace BBDevPulse.Logic;

/// <summary>
/// Loads people metadata from CSV file.
/// </summary>
internal sealed class PeopleCsvProvider : IPeopleCsvProvider
{
    private const string BasicHeader = "Name;Grade;Department";

    /// <summary>
    /// Initializes a new instance of the <see cref="PeopleCsvProvider"/> class.
    /// </summary>
    /// <param name="options">Bitbucket options.</param>
    public PeopleCsvProvider(IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _peopleCsvPath = string.IsNullOrWhiteSpace(options.Value.PeopleCsvPath)
            ? null
            : options.Value.PeopleCsvPath.Trim();
    }

    /// <inheritdoc />
    public async Task<Dictionary<DisplayName, PersonCsvRow>> GetPeopleByDisplayNameAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_peopleCsvPath))
        {
#pragma warning disable IDE0028 // Simplify collection initialization
            return new(DisplayNameValueComparer.Instance);
#pragma warning restore IDE0028 // Simplify collection initialization
        }

        if (!File.Exists(_peopleCsvPath))
        {
            return new(DisplayNameValueComparer.Instance);
        }

        try
        {
            return await ReadPeopleAsync(_peopleCsvPath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return new(DisplayNameValueComparer.Instance);
        }
        catch (UnauthorizedAccessException)
        {
            return new(DisplayNameValueComparer.Instance);
        }
    }

    private static async Task<Dictionary<DisplayName, PersonCsvRow>> ReadPeopleAsync(
        string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);
        return await ReadPeopleAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<Dictionary<DisplayName, PersonCsvRow>> ReadPeopleAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        var peopleByName = new Dictionary<DisplayName, PersonCsvRow>(DisplayNameValueComparer.Instance);
        var entries = new List<(string Name, PersonCsvRow Row, int Line)>();
        var lineNumber = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmedLine = line.Trim();
            if (lineNumber == 1 && trimmedLine.Equals(BasicHeader + ";UUID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmedLine.Split(';');
            if (parts.Length != 4)
            {
                throw new PeopleCsvFormatException(
                    $"Invalid people CSV format at line {lineNumber}. Expected '{BasicHeader};UUID'.");
            }

            var name = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PeopleCsvFormatException($"Missing name at line {lineNumber}.");
            }

            var grade = string.IsNullOrWhiteSpace(parts[1]) ? DeveloperStats.NOT_AVAILABLE : parts[1].Trim();
            var department = string.IsNullOrWhiteSpace(parts[2]) ? DeveloperStats.NOT_AVAILABLE : parts[2].Trim();
            var uuid = parts[3].Trim();
            if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(uuid.Trim('{', '}')))
            {
                throw new PeopleCsvFormatException($"Missing UUID at line {lineNumber}.");
            }

            entries.Add((name, new PersonCsvRow(grade, department, uuid), lineNumber));
        }

        var uuids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            // Each entry has a UUID validated while reading its line.
            var uuidKey = entry.Row.Uuid!.Trim().Trim('{', '}');
            if (uuids.TryGetValue(uuidKey, out var previousLine))
            {
                throw new PeopleCsvFormatException(
                    $"Duplicate UUID '{entry.Row.Uuid}' at lines {previousLine} and {entry.Line}. Each UUID must be unique in the CSV.");
            }
            uuids.Add(uuidKey, entry.Line);
        }

        foreach (var entry in entries)
        {
            var key = new DisplayName(entry.Name);
            if (peopleByName.TryGetValue(key, out var previous))
            {
                peopleByName[key] = previous with
                {
                    AdditionalRows = [.. previous.Rows.Skip(1), entry.Row]
                };
            }
            else
            {
                peopleByName[key] = entry.Row;
            }
        }

        return peopleByName;
    }

    private readonly string? _peopleCsvPath;

    private sealed class DisplayNameValueComparer : IEqualityComparer<DisplayName>
    {
        public static DisplayNameValueComparer Instance { get; } = new();

        public bool Equals(DisplayName? x, DisplayName? y) =>
            string.Equals(x?.Value, y?.Value, StringComparison.Ordinal);

        public int GetHashCode(DisplayName obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return StringComparer.Ordinal.GetHashCode(obj.Value);
        }
    }
}

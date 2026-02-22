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
    public Dictionary<DisplayName, PersonCsvRow> GetPeopleByDisplayName()
    {
        if (string.IsNullOrWhiteSpace(_peopleCsvPath))
        {
#pragma warning disable IDE0028 // Simplify collection initialization
            return new(DisplayNameValueComparer.Instance);
#pragma warning restore IDE0028 // Simplify collection initialization
        }

        if (!File.Exists(_peopleCsvPath))
        {
            throw new FileNotFoundException($"People CSV file '{_peopleCsvPath}' was not found.", _peopleCsvPath);
        }

        var peopleByName = new Dictionary<DisplayName, PersonCsvRow>(DisplayNameValueComparer.Instance);
        var lineNumber = 0;
        foreach (var line in File.ReadLines(_peopleCsvPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmedLine = line.Trim();
            if (lineNumber == 1 && trimmedLine.Equals("Name;Grade;Department", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmedLine.Split(';');
            if (parts.Length != 3)
            {
                throw new FormatException(
                    $"Invalid people CSV format at line {lineNumber}. Expected 'Name;Grade;Department'.");
            }

            var name = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var grade = string.IsNullOrWhiteSpace(parts[1]) ? DeveloperStats.NotAvailable : parts[1].Trim();
            var department = string.IsNullOrWhiteSpace(parts[2]) ? DeveloperStats.NotAvailable : parts[2].Trim();
            peopleByName[new DisplayName(name)] = new PersonCsvRow(grade, department);
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

namespace BBDevPulse.CsvUuidLookup;

internal sealed record CsvRow(int Line, string[] Fields);
internal sealed record CsvIssue(int Line, string Name, string Message);
internal sealed record CsvResult(IReadOnlyList<string> Lines, IReadOnlyList<CsvIssue> Issues, int Filled);

internal static class CsvResolver
{
    private const string Header = "Name;Grade;Department";

    public static CsvResult Resolve(IReadOnlyList<string> lines, IReadOnlyList<WorkspaceMember> members)
    {
        var rows = new List<CsvRow>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;
            var trimmed = lines[i].Trim();
            if (i == 0 && (trimmed.Equals(Header, StringComparison.OrdinalIgnoreCase) ||
                           trimmed.Equals(Header + ";UUID", StringComparison.OrdinalIgnoreCase)))
                continue;
            var fields = lines[i].Split(';');
            if ((fields.Length != 3 && fields.Length != 4) || string.IsNullOrWhiteSpace(fields[0]))
                throw new InvalidDataException($"Invalid CSV at line {i + 1}: expected Name;Grade;Department with an optional UUID.");
            rows.Add(new CsvRow(i + 1, fields));
        }

        var byName = members.GroupBy(member => member.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var proposed = new Dictionary<int, string>();
        var issues = new List<CsvIssue>();
        foreach (var row in rows)
        {
            if (row.Fields.Length == 4 && !string.IsNullOrWhiteSpace(row.Fields[3]))
                continue;
            var name = row.Fields[0].Trim();
            if (!byName.TryGetValue(name, out var matches))
            {
                issues.Add(new CsvIssue(row.Line, name, "no workspace member with this display name"));
                continue;
            }
            var ids = matches.Where(member => member.Uuid is not null)
                .GroupBy(member => Normalize(member.Uuid!), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First().Uuid!).ToArray();
            if (matches.Any(member => member.Uuid is null) || ids.Length != 1 || Normalize(ids[0]).Length == 0)
            {
                issues.Add(new CsvIssue(row.Line, name, $"ambiguous or missing UUID in Bitbucket ({matches.Length} matching member entries: {string.Join(", ", ids)})"));
                continue;
            }
            proposed.Add(row.Line, ids[0]);
        }
        var uses = rows.Select(row => (row, uuid: row.Fields.Length == 4 && !string.IsNullOrWhiteSpace(row.Fields[3])
                ? row.Fields[3] : proposed.GetValueOrDefault(row.Line)))
            .Where(item => !string.IsNullOrWhiteSpace(item.uuid))
            .GroupBy(item => Normalize(item.uuid!), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length == 0 || group.Count() > 1);
        foreach (var group in uses)
        {
            foreach (var item in group)
            {
                proposed.Remove(item.row.Line);
                issues.Add(new CsvIssue(item.row.Line, item.row.Fields[0].Trim(),
                    $"UUID is empty or would be duplicated in CSV (lines {string.Join(", ", group.Select(x => x.row.Line))})"));
            }
        }
        var output = lines.ToArray();
        if (output.Length > 0 && output[0].Trim().Equals(Header, StringComparison.OrdinalIgnoreCase))
            output[0] += ";UUID";
        foreach (var row in rows)
        {
            var uuid = row.Fields.Length == 4 ? row.Fields[3] : "";
            if (proposed.TryGetValue(row.Line, out var found))
                uuid = found;
            output[row.Line - 1] = string.Join(';', row.Fields.Take(3)) + ";" + uuid;
        }
        return new CsvResult(output, issues.OrderBy(issue => issue.Line).ToArray(), proposed.Count);
    }

    private static string Normalize(string uuid) => uuid.Trim().Trim('{', '}');
}

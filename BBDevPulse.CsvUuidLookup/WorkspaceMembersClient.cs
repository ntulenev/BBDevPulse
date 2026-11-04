using System.Net.Http.Headers;
using System.Text.Json;

namespace BBDevPulse.CsvUuidLookup;

internal sealed record WorkspaceMember(string Name, string? Uuid);

internal sealed class WorkspaceMembersClient(HttpClient client)
{
    private const string ApiHost = "api.bitbucket.org";

    public async Task<IReadOnlyList<WorkspaceMember>> GetMembersAsync(string workspace, CancellationToken cancellationToken)
    {
        var url = new Uri($"https://{ApiHost}/2.0/workspaces/{Uri.EscapeDataString(workspace)}/members?pagelen=100");
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var members = new List<WorkspaceMember>();
        while (url is not null)
        {
            if (url.Scheme != Uri.UriSchemeHttps || !url.Host.Equals(ApiHost, StringComparison.OrdinalIgnoreCase) ||
                url.Port != 443 || !string.IsNullOrEmpty(url.UserInfo) || !visited.Add(url.AbsoluteUri))
            {
                throw new InvalidDataException("Bitbucket returned an invalid or repeated pagination URL.");
            }
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Bitbucket member request failed (HTTP {(int)response.StatusCode}). Check workspace access and token permissions.");
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Bitbucket member response has no values array.");
            foreach (var membership in values.EnumerateArray())
            {
                if (!membership.TryGetProperty("user", out var user) || user.ValueKind != JsonValueKind.Object ||
                    !user.TryGetProperty("display_name", out var name) || name.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(name.GetString()))
                    throw new InvalidDataException("Bitbucket returned a member without a display name; name matching would be unsafe.");
                string? uuid = null;
                if (user.TryGetProperty("uuid", out var id) && id.ValueKind == JsonValueKind.String)
                    uuid = id.GetString();
                members.Add(new WorkspaceMember(name.GetString()!.Trim(), string.IsNullOrWhiteSpace(uuid) ? null : uuid.Trim()));
            }
            if (!root.TryGetProperty("next", out var next) || next.ValueKind == JsonValueKind.Null)
                url = null;
            else if (next.ValueKind != JsonValueKind.String || !Uri.TryCreate(next.GetString(), UriKind.Absolute, out url))
                throw new InvalidDataException("Bitbucket returned an invalid pagination URL.");
        }
        return members;
    }
}

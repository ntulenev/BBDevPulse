using System.Text.Json;

using BBDevPulse.Abstractions;
using BBDevPulse.Models;

namespace BBDevPulse.API.Mappers;

/// <summary>
/// Maps Bitbucket activity payloads into domain models.
/// </summary>
public sealed class PullRequestActivityMapper : IPullRequestActivityMapper
{
    /// <inheritdoc />
    public PullRequestActivity Map(JsonElement activity)
    {
        if (activity.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("Activity payload must be defined.", nameof(activity));
        }

        var activityDate = TryGetActivityDate(activity, out var resolvedDate) ? resolvedDate : (DateTimeOffset?)null;
        var mergeDate = TryGetMergeDate(activity, out var resolvedMergeDate)
            ? resolvedMergeDate
            : (DateTimeOffset?)null;
        var actor = TryGetActivityUser(activity);
        var comment = TryGetCommentInfo(activity, out var commentUser, out var commentDate)
            ? new ActivityComment(commentUser, commentDate)
            : null;
        var approval = TryGetApprovalInfo(activity, out var approvalUser, out var approvalDate)
            ? new ActivityApproval(approvalUser, approvalDate)
            : null;

        return new PullRequestActivity(activityDate, mergeDate, actor, comment, approval);
    }

    private static bool TryGetActivityDate(JsonElement activity, out DateTimeOffset date)
    {
        if (TryGetDate(activity, out date, "comment", "created_on"))
        {
            return true;
        }

        if (TryGetDate(activity, out date, "approval", "date"))
        {
            return true;
        }

        if (TryGetDate(activity, out date, "approval", "approved_on"))
        {
            return true;
        }

        if (TryGetDate(activity, out date, "update", "date"))
        {
            return true;
        }

        if (TryGetDate(activity, out date, "pullrequest", "updated_on"))
        {
            return true;
        }

        if (TryGetDate(activity, out date, "pullrequest", "created_on"))
        {
            return true;
        }

        if (TryGetDate(activity, out date, "created_on"))
        {
            return true;
        }

        if (TryGetDate(activity, out date, "date"))
        {
            return true;
        }

        return TryGetDate(activity, out date, "updated_on");
    }

    private static bool TryGetMergeDate(JsonElement activity, out DateTimeOffset date)
    {
        if (TryGetString(activity, out var state, "pullrequest", "state") &&
            string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDate(activity, out date, "pullrequest", "merged_on"))
            {
                return true;
            }

            if (TryGetDate(activity, out date, "pullrequest", "updated_on"))
            {
                return true;
            }

            if (TryGetDate(activity, out date, "date"))
            {
                return true;
            }

            if (TryGetDate(activity, out date, "created_on"))
            {
                return true;
            }
        }

        if (TryGetString(activity, out state, "update", "state") &&
            string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDate(activity, out date, "update", "date"))
            {
                return true;
            }

            if (TryGetDate(activity, out date, "date"))
            {
                return true;
            }
        }

        if (TryGetDate(activity, out date, "merge", "date"))
        {
            return true;
        }

        if (TryGetDate(activity, out date, "merge", "created_on"))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static bool TryGetCommentInfo(JsonElement activity, out DeveloperIdentity user, out DateTimeOffset date)
    {
        if (TryGetCommentInfoForPath(activity, out user, out date, "comment"))
        {
            return true;
        }

        if (TryGetCommentInfoForPath(activity, out user, out date, "pullrequest_comment"))
        {
            return true;
        }

        if (TryGetCommentInfoForPath(activity, out user, out date, "pull_request_comment"))
        {
            return true;
        }

        user = default;
        date = default;
        return false;
    }

    private static bool TryGetCommentInfoForPath(
        JsonElement activity,
        out DeveloperIdentity user,
        out DateTimeOffset date,
        params string[] path)
    {
        if (TryGetUser(activity, out user, [.. path, "user"]))
        {
            if (TryGetDate(activity, out date, [.. path, "created_on"]) ||
                TryGetDate(activity, out date, [.. path, "updated_on"]) ||
                TryGetDate(activity, out date, "date") ||
                TryGetDate(activity, out date, "created_on"))
            {
                return true;
            }
        }

        user = default;
        date = default;
        return false;
    }

    private static bool TryGetApprovalInfo(JsonElement activity, out DeveloperIdentity user, out DateTimeOffset date)
    {
        if (TryGetUser(activity, out user, "approval", "user") &&
            (TryGetDate(activity, out date, "approval", "date") ||
             TryGetDate(activity, out date, "approval", "approved_on") ||
             TryGetDate(activity, out date, "date")))
        {
            return true;
        }

        user = default;
        date = default;
        return false;
    }

    private static DeveloperIdentity? TryGetActivityUser(JsonElement activity)
    {
        if (TryGetUser(activity, out var user, "comment", "user"))
        {
            return user;
        }

        if (TryGetUser(activity, out user, "approval", "user"))
        {
            return user;
        }

        if (TryGetUser(activity, out user, "update", "author"))
        {
            return user;
        }

        if (TryGetUser(activity, out user, "update", "user"))
        {
            return user;
        }

        if (TryGetUser(activity, out user, "pullrequest", "author"))
        {
            return user;
        }

        if (TryGetUser(activity, out user, "actor"))
        {
            return user;
        }

        return TryGetUser(activity, out user, "user") ? user : null;
    }

    private static bool TryGetString(JsonElement element, out string value, params string[] path)
    {
        if (TryGetNestedProperty(element, out var stringElement, path) &&
            stringElement.ValueKind == JsonValueKind.String)
        {
            value = stringElement.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetUser(JsonElement element, out DeveloperIdentity user, params string[] path)
    {
        if (TryGetNestedProperty(element, out var userElement, path))
        {
            var displayName = GetString(userElement, "display_name") ??
                GetString(userElement, "nickname") ??
                GetString(userElement, "username");
            var uuid = GetString(userElement, "uuid");

            if (!string.IsNullOrWhiteSpace(displayName) || !string.IsNullOrWhiteSpace(uuid))
            {
                var resolvedName = !string.IsNullOrWhiteSpace(displayName)
                    ? displayName
                    : uuid ?? "unknown";
                var resolvedUuid = string.IsNullOrWhiteSpace(uuid) ? null : new UserUuid(uuid);
                user = new DeveloperIdentity(
                    resolvedUuid,
                    new DisplayName(resolvedName));
                return true;
            }
        }

        user = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static bool TryGetDate(JsonElement element, out DateTimeOffset date, params string[] path)
    {
        if (TryGetNestedProperty(element, out var dateElement, path) &&
            dateElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(dateElement.GetString(), out date))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static bool TryGetNestedProperty(JsonElement element, out JsonElement result, params string[] path)
    {
        result = element;
        foreach (var segment in path)
        {
            if (result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty(segment, out var next))
            {
                return false;
            }

            result = next;
        }

        return true;
    }
}

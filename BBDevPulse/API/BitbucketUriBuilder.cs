using BBDevPulse.Abstractions;

namespace BBDevPulse.API;

/// <summary>
/// Builds Bitbucket API URIs with optional partial response field selection.
/// </summary>
internal sealed class BitbucketUriBuilder : IBitbucketUriBuilder
{
    /// <inheritdoc />
    public Uri BuildRelativeUri(string path, BitbucketFieldGroup fieldGroup = BitbucketFieldGroup.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_fieldGroups.TryGetValue(fieldGroup, out var fields) || fields.Length == 0)
        {
            return new Uri(path, UriKind.Relative);
        }

        var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var fieldsValue = Uri.EscapeDataString(string.Join(",", fields));
        return new Uri($"{path}{separator}fields={fieldsValue}", UriKind.Relative);
    }

    private static readonly Dictionary<BitbucketFieldGroup, string[]> _fieldGroups =
        new Dictionary<BitbucketFieldGroup, string[]>
        {
            [BitbucketFieldGroup.None] = [],
            [BitbucketFieldGroup.RepositoryList] =
            [
                "next",
                "values.name",
                "values.slug"
            ],
            [BitbucketFieldGroup.PullRequestList] =
            [
                "next",
                "values.id",
                "values.state",
                "values.closed_on",
                "values.created_on",
                "values.updated_on",
                "values.merged_on",
                "values.author.display_name",
                "values.author.uuid",
                "values.source.commit.hash",
                "values.destination.commit.hash",
                "values.destination.branch.name"
            ],
            [BitbucketFieldGroup.PullRequestActivity] =
            [
                "next",
                "values.created_on",
                "values.updated_on",
                "values.date",
                "values.comment.created_on",
                "values.comment.updated_on",
                "values.comment.user.display_name",
                "values.comment.user.nickname",
                "values.comment.user.username",
                "values.comment.user.uuid",
                "values.approval.date",
                "values.approval.approved_on",
                "values.approval.user.display_name",
                "values.approval.user.nickname",
                "values.approval.user.username",
                "values.approval.user.uuid",
                "values.update.date",
                "values.update.state",
                "values.update.author.display_name",
                "values.update.author.nickname",
                "values.update.author.username",
                "values.update.author.uuid",
                "values.update.user.display_name",
                "values.update.user.nickname",
                "values.update.user.username",
                "values.update.user.uuid",
                "values.pullrequest.created_on",
                "values.pullrequest.updated_on",
                "values.pullrequest.merged_on",
                "values.pullrequest.state",
                "values.pullrequest.author.display_name",
                "values.pullrequest.author.nickname",
                "values.pullrequest.author.username",
                "values.pullrequest.author.uuid",
                "values.pullrequest_comment.created_on",
                "values.pullrequest_comment.updated_on",
                "values.pullrequest_comment.user.display_name",
                "values.pullrequest_comment.user.nickname",
                "values.pullrequest_comment.user.username",
                "values.pullrequest_comment.user.uuid",
                "values.pull_request_comment.created_on",
                "values.pull_request_comment.updated_on",
                "values.pull_request_comment.user.display_name",
                "values.pull_request_comment.user.nickname",
                "values.pull_request_comment.user.username",
                "values.pull_request_comment.user.uuid",
                "values.actor.display_name",
                "values.actor.nickname",
                "values.actor.username",
                "values.actor.uuid",
                "values.user.display_name",
                "values.user.nickname",
                "values.user.username",
                "values.user.uuid",
                "values.merge.date",
                "values.merge.created_on"
            ],
            [BitbucketFieldGroup.PullRequestCommit] =
            [
                "next",
                "values.hash",
                "values.date",
                "values.summary.raw"
            ],
            [BitbucketFieldGroup.PullRequestSizeReference] =
            [
                "source.commit.hash",
                "destination.commit.hash"
            ],
            [BitbucketFieldGroup.PullRequestDiffStat] =
            [
                "next",
                "values.lines_added",
                "values.lines_removed"
            ]
        };
}

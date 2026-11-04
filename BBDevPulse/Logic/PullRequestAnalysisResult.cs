using BBDevPulse.Models;

namespace BBDevPulse.Logic;

// A completed, per-PR contribution. Its collections are detached from the analysis process.
internal sealed class PullRequestAnalysisResult
{
    private PullRequestAnalysisResult(Workspace workspace, RepoSlug repository, PullRequestId id,
        IReadOnlyList<DeveloperIdentity> observedIdentities)
    {
        Workspace = workspace;
        Repository = repository;
        Id = id;
        ObservedIdentities = Array.AsReadOnly(observedIdentities.ToArray());
    }

    public PullRequestAnalysisResult(
        Workspace workspace, Repository repository, PullRequest pr,
        IReadOnlyList<DeveloperIdentity> observedIdentities, DeveloperIdentity? author,
        bool includeAuthoredPullRequest, PullRequestReport report, DateTimeOffset? mergedOn,
        int corrections, bool hasCommits, ActivityAnalysisState analysis,
        IReadOnlyList<DeveloperCommitActivity> commits,
        IReadOnlyList<(DeveloperIdentity User, DateTimeOffset Date)>? comments,
        IReadOnlyList<(DeveloperIdentity User, DateTimeOffset Date)>? approvals)
        : this(workspace, repository.Slug, pr.Id, observedIdentities)
    {
        Author = author;
        IncludeAuthoredPullRequest = includeAuthoredPullRequest;
        Report = report;
        CreatedOn = pr.CreatedOn;
        MergedOn = mergedOn;
        Corrections = corrections;
        HasCommits = hasCommits;
        Participants = new System.Collections.ObjectModel.ReadOnlyDictionary<string, DeveloperIdentity>(
            new Dictionary<string, DeveloperIdentity>(analysis.Participants, StringComparer.OrdinalIgnoreCase));
        CommentCounts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(analysis.CommentCounts, StringComparer.OrdinalIgnoreCase));
        ApprovalCounts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(analysis.ApprovalCounts, StringComparer.OrdinalIgnoreCase));
        Commits = Array.AsReadOnly(commits.ToArray());
        Comments = comments is null ? null : Array.AsReadOnly(comments.ToArray());
        Approvals = approvals is null ? null : Array.AsReadOnly(approvals.ToArray());
    }

    public static PullRequestAnalysisResult Skipped(Workspace workspace, RepoSlug repository,
        PullRequestId id, IReadOnlyList<DeveloperIdentity> observedIdentities) =>
        new(workspace, repository, id, observedIdentities);

    public Workspace Workspace { get; }
    public RepoSlug Repository { get; }
    public PullRequestId Id { get; }
    public IReadOnlyList<DeveloperIdentity> ObservedIdentities { get; }
    public DeveloperIdentity? Author { get; }
    public bool IncludeAuthoredPullRequest { get; }
    public PullRequestReport? Report { get; }
    public DateTimeOffset CreatedOn { get; }
    public DateTimeOffset? MergedOn { get; }
    public int Corrections { get; }
    public bool HasCommits { get; }
    public IReadOnlyDictionary<string, DeveloperIdentity> Participants { get; } = new Dictionary<string, DeveloperIdentity>();
    public IReadOnlyDictionary<string, int> CommentCounts { get; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ApprovalCounts { get; } = new Dictionary<string, int>();
    public IReadOnlyList<DeveloperCommitActivity> Commits { get; } = [];
    public IReadOnlyList<(DeveloperIdentity User, DateTimeOffset Date)>? Comments { get; }
    public IReadOnlyList<(DeveloperIdentity User, DateTimeOffset Date)>? Approvals { get; }
}

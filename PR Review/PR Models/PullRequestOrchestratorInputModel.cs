namespace Kemibrug.AI.Assistant.Models.PR_Review
{
    /// <summary>
    /// Defines the initial input required to start the Pull Request analysis orchestration.
    /// </summary>
    public sealed class PullRequestOrchestratorInput
    {
        public int PullRequestId { get; init; }
        public string RepositoryId { get; init; } = string.Empty;
        public string ProjectId { get; init; } = string.Empty;
        public string SourceRefName { get; init; } = string.Empty;
    }
}
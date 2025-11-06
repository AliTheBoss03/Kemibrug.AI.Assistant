namespace Kemibrug.AI.Assistant.Models.PR_Review
{
    /// <summary>
    /// Defines the input for the activity that posts a comment to a pull request.
    /// </summary>
    public sealed class PostAnalysisCommentInput
    {
        public int PullRequestId { get; init; }
        public string RepositoryId { get; init; } = string.Empty;
        public string ProjectId { get; init; } = string.Empty;
        public string AnalysisJson { get; init; } = "{}";
    }
}
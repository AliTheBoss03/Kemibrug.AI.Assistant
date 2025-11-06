using System.Text.Json.Serialization;

namespace Kemibrug.AI.Assistant.Models.AzureDevOps
{
    public sealed class PullRequestWebhookPayload
    {
        [JsonPropertyName("resource")]
        public Resource Resource { get; init; } = new();
    }

    public sealed class Resource
    {
        [JsonPropertyName("repository")]
        public Repository Repository { get; init; } = new();

        [JsonPropertyName("pullRequestId")]
        public int PullRequestId { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("sourceRefName")]
        public string SourceRefName { get; init; } = string.Empty;
    }

    public sealed class Repository
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("project")]
        public Project Project { get; init; } = new();
    }

    public sealed class Project
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;
    }
}
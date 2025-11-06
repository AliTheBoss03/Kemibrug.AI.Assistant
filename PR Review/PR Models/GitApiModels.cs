using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kemibrug.AI.Assistant.Models.AzureDevOps
{
    public sealed class CommitListResponse
    {
        [JsonPropertyName("value")]
        public List<Commit> Value { get; init; } = new();
    }

    public sealed class Commit
    {
        [JsonPropertyName("commitId")]
        public string CommitId { get; init; } = string.Empty;
    }

    public sealed class CommitChangesResponse
    {
        [JsonPropertyName("changes")]
        public List<Change> Changes { get; init; } = new();
    }

    public sealed class Change
    {
        [JsonPropertyName("item")]
        public Item Item { get; init; } = new();

        [JsonPropertyName("changeType")]
        public string ChangeType { get; init; } = string.Empty;
    }

    public sealed class Item
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;
    }
}
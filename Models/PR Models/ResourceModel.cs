using Kemibrug.AI.Assistant.Models;
using System.Text.Json.Serialization;

public class Resource
{
    [JsonPropertyName("repository")]
    public Repository Repository { get; set; }

    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}
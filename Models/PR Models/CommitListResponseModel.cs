using System.Text.Json.Serialization;

public class CommitListResponse
{
    [JsonPropertyName("value")]
    public List<Commit> Value { get; set; }
}
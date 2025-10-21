using System.Text.Json.Serialization;

public class Commit
{
    [JsonPropertyName("commitId")]
    public string CommitId { get; set; }
}
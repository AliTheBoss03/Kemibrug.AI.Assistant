using System.Text.Json.Serialization;

public class CommitChangesResponse
{
    [JsonPropertyName("changes")]
    public List<Change> Changes { get; set; }
}
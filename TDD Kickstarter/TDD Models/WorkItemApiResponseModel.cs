using System.Text.Json.Serialization;

public class WorkItemApiResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fields")]
    public WorkItemFields Fields { get; set; }
}
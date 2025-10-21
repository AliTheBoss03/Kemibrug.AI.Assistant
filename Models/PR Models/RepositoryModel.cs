using System.Text.Json.Serialization;

public class Repository
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
}
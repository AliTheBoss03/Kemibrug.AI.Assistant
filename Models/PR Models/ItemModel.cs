using System.Text.Json.Serialization;

public class Item
{
    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}
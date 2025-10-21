using System.Text.Json.Serialization;

public class Change
{
    [JsonPropertyName("item")]
    public Item Item { get; set; }

    [JsonPropertyName("changeType")]
    public string ChangeType { get; set; }
}
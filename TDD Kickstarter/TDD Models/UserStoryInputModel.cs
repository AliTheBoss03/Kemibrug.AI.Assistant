using System.Text.Json.Serialization;

public class UserStoryInput
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; }
}
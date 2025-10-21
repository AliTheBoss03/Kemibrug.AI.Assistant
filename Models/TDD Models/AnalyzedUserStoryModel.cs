using System.Text.Json.Serialization;

public class AnalyzedUserStory
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; }

    [JsonPropertyName("testMethods")]
    public List<string> TestMethods { get; set; }
}
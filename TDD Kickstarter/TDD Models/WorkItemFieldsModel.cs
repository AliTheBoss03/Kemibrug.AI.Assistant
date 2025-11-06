using System.Text.Json.Serialization;

public class WorkItemFields
{
    [JsonPropertyName("System.Title")]
    public string Title { get; set; }

    [JsonPropertyName("System.Description")]
    public string Description { get; set; }

    [JsonPropertyName("Microsoft.VSTS.Common.AcceptanceCriteria")]
    public string AcceptanceCriteriaHtml { get; set; }
}
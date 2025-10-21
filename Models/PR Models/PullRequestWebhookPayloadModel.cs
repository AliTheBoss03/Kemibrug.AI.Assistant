using System.Text.Json.Serialization;

namespace Kemibrug.AI.Assistant.Models
{
    public class PullRequestWebhookPayload
    {
        [JsonPropertyName("resource")]
        public Resource Resource { get; set; }
    }
}
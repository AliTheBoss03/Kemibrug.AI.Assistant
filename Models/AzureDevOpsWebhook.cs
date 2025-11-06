using Microsoft.AspNetCore;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace Kemibrug.AI.Assistant.Models
{
    public class AzureDevOpsWebhook
    {
        [JsonPropertyName("resource")]
        public WebhookResource? Resource { get; set; }
    }

    public class WebhookResource
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }
}
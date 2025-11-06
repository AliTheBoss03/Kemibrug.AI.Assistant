using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Kemibrug.AI.Assistant.Models
{
    public class ResourceContainer
    {
        [JsonPropertyName("workItemId")]
        public int? WorkItemId { get; set; }

        // Including 'Id' from the resource object as a fallback
        [JsonPropertyName("id")]
        public int? ResourceId { get; set; }
    }
}

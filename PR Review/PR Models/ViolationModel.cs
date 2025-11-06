using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kemibrug.AI.Assistant.Models.PR_Models
{
    public sealed class Violation
    {
        [System.Text.Json.Serialization.JsonPropertyName("rule")]
        public string? Rule { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("principle")]
        public string? Principle { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("severity")]
        public string? Severity { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("lines")]
        public List<int>? Lines { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("evidence")]
        public string? Evidence { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("suggestion")]
        public string? Suggestion { get; init; }
    }
}

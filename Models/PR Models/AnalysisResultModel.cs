using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kemibrug.AI.Assistant.Models.PR_Models
{
    public sealed class AnalysisResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("violationFound")]
        public bool? ViolationFound { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("explanation")]
        public string? Explanation { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("violations")]
        public List<Violation> Violations { get; init; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("meta")]
        public Dictionary<string, object>? Meta { get; init; }
    }
}

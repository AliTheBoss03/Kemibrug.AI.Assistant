using Kemibrug.AI.Assistant.Models.PR_Models;
using Kemibrug.AI.Assistant.Models.PR_Review;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;

namespace Kemibrug.AI.Assistant.PR_Review
{
    public interface IAnalysisResultParser
    {
        AnalysisResult Parse(string json);
    }

    public class AnalysisResultParser : IAnalysisResultParser
    {
        private readonly ILogger<AnalysisResultParser> _logger;

        public AnalysisResultParser(ILogger<AnalysisResultParser> logger)
        {
            _logger = logger;
        }

        public AnalysisResult Parse(string json)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<AnalysisResult>(json, options);

                return result ?? new AnalysisResult { ViolationFound = null, Explanation = "AI model returned an empty response." };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse JSON from the analysis model. Content: {json}", json);
                return new AnalysisResult { ViolationFound = null, Explanation = "AI model response was not valid JSON." };
            }
        }
    }
}
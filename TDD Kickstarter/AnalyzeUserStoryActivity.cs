using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Kemibrug.AI.Assistant.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant
{
    public class AnalyzeUserStoryActivity
    {
        private readonly ILogger<AnalyzeUserStoryActivity> _logger;
        private readonly OpenAIClient _openAIClient;
        private readonly IHeuristicAnalyzer _heuristicAnalyzer;

        public AnalyzeUserStoryActivity(ILogger<AnalyzeUserStoryActivity> logger, OpenAIClient openAIClient, IHeuristicAnalyzer heuristicAnalyzer)
        {
            _logger = logger;
            _openAIClient = openAIClient;
            _heuristicAnalyzer = heuristicAnalyzer;
        }

        [Function(nameof(AnalyzeUserStoryActivity))]
        public async Task<AnalyzedUserStory> Run([ActivityTrigger] UserStoryInput userStory)
        {
            if (userStory is null)
            {
                _logger.LogWarning("UserStoryInput was null. Returning empty result.");
                return new AnalyzedUserStory { ClassName = "InvalidInput", TestMethods = new List<string>() };
            }

            if (_openAIClient == null)
            {
                _logger.LogError("OpenAIClient is not configured or available. Cannot perform analysis.");
                throw new InvalidOperationException("OpenAI service is not configured in the application.");
            }

            var deploymentName = "gpt-4o";
            if (string.IsNullOrEmpty(deploymentName))
            {
                throw new InvalidOperationException("Configuration error: AzureOpenAIDeploymentName is not set.");
            }

            try
            {
                var systemPrompt =
                    @"Role: Senior test-automation engineer.
                    Task: Convert a User Story into a test plan.
                    You must return STRICTLY formatted JSON of the following schema: { ""className"": string, ""testMethods"": [string,...] }";

                var userPayload = JsonSerializer.Serialize(userStory);

                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    Temperature = 0.1f,
                    MaxTokens = 500,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
                    DeploymentName = deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage(systemPrompt),
                        new ChatRequestUserMessage(userPayload)
                    }
                };

                Response<ChatCompletions> response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                string content = response.Value.Choices[0].Message.Content ?? "{}";

                var analyzedStory = ParseAiResponse(content);

                if (string.IsNullOrWhiteSpace(analyzedStory.ClassName) || analyzedStory.TestMethods.Count == 0)
                {
                    _logger.LogWarning("AI analysis returned an empty or invalid result. Falling back to local heuristic.");
                    return _heuristicAnalyzer.Analyze(userStory);
                }

                _logger.LogInformation("Successfully analyzed User Story '{title}' into class '{className}'.", userStory.Title, analyzedStory.ClassName);
                return analyzedStory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during AI analysis for User Story '{title}'.", userStory.Title);
                throw;
            }
        }

        private AnalyzedUserStory ParseAiResponse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var className = root.TryGetProperty("className", out var cn) ? cn.GetString() ?? "" : "";
                var methods = new List<string>();
                if (root.TryGetProperty("testMethods", out var tm) && tm.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in tm.EnumerateArray())
                    {
                        var s = el.GetString() ?? "";
                        // Use the new static sanitizer class
                        if (!string.IsNullOrWhiteSpace(s)) methods.Add(CodeNamingSanitizer.ToSafeMethodName(s));
                    }
                }

                return new AnalyzedUserStory
                {
                    ClassName = CodeNamingSanitizer.ToSafeClassName(string.IsNullOrWhiteSpace(className) ? "GeneratedTests" : className),
                    TestMethods = methods.Distinct().ToList()
                };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "ParseAiResponse: Invalid JSON received from AI. Content: {json}", json);
                return new AnalyzedUserStory { ClassName = "", TestMethods = new List<string>() };
            }
        }
    }
}
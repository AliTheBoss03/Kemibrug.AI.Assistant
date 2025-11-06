using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Kemibrug.AI.Assistant.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant
{
    public static class AnalyzeUserStoryActivity
    {
        [Function("AnalyzeUserStoryActivity")]
        public static async Task<AnalyzedUserStory> Run(
            [ActivityTrigger] UserStoryInput userStory,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger("AnalyzeUserStoryActivity");
            if (userStory is null)
                return new AnalyzedUserStory { ClassName = "GeneratedTests", TestMethods = new List<string>() };

            var (endpoint, key, deployment) = TddHelpers.GetOpenAIConfig();
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                log.LogWarning("OpenAI not configured; using heuristic analysis.");
                return TddHelpers.LocalHeuristicAnalyze(userStory);
            }

            try
            {
                var client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));

                var systemPrompt =
                    @"Role: Senior test-automation engineer.
                    Task: Convert a User Story into a test plan.
                       Return STRICT JSON: { ""className"": string, ""testMethods"": [string,...] }";

                var payload = JsonSerializer.Serialize(new
                {
                    Title = userStory.Title ?? "",
                    Description = userStory.Description ?? "",
                    AcceptanceCriteria = userStory.AcceptanceCriteria ?? new List<string>()
                });

                var opts = new ChatCompletionsOptions
                {
                    Temperature = 0.1f,
                    MaxTokens = 500,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
                    DeploymentName = deployment
                };
                opts.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
                opts.Messages.Add(new ChatRequestUserMessage(payload));

                var resp = await client.GetChatCompletionsAsync(opts);
                var content = resp.Value.Choices[0].Message.Content ?? "{}";

                var analyzed = TddHelpers.ParseAnalyzed(content, log);
                if (string.IsNullOrWhiteSpace(analyzed.ClassName) || analyzed.TestMethods.Count == 0)
                    analyzed = TddHelpers.LocalHeuristicAnalyze(userStory);

                return analyzed;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "AnalyzeUserStory failed; using heuristic.");
                return TddHelpers.LocalHeuristicAnalyze(userStory);
            }
        }
    }
}

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
    public static class GenerateTestSkeletonActivity
    {
        [Function("GenerateTestSkeletonActivity")]
        public static async Task<string> Run(
            [ActivityTrigger] AnalyzedUserStory analyzed,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger("GenerateTestSkeletonActivity");
            if (analyzed is null || string.IsNullOrWhiteSpace(analyzed.ClassName))
                return TddHelpers.BuildCSharpTestSkeleton("GeneratedTests", Array.Empty<string>());

            var (endpoint, key, deployment) = TddHelpers.GetOpenAIConfig();
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
                return TddHelpers.BuildCSharpTestSkeleton(analyzed.ClassName, analyzed.TestMethods);

            try
            {
                var client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));

                var systemPrompt =
                    @"Role: Senior C# engineer.
                    Task: Generate a complete xUnit test class skeleton.
                    Return STRICT JSON: { ""fileName"": string, ""fileContent"": string }";

                var plan = JsonSerializer.Serialize(new
                {
                    className = analyzed.ClassName,
                    testMethods = analyzed.TestMethods ?? new List<string>()
                });

                var opts = new ChatCompletionsOptions
                {
                    Temperature = 0.1f,
                    MaxTokens = 1200,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
                    DeploymentName = deployment
                };
                opts.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
                opts.Messages.Add(new ChatRequestUserMessage(plan));

                var resp = await client.GetChatCompletionsAsync(opts);
                var content = resp.Value.Choices[0].Message.Content ?? "{}";

                var (_, fileContent) = TddHelpers.ParseFileResult(content, log);
                return string.IsNullOrWhiteSpace(fileContent)
                    ? TddHelpers.BuildCSharpTestSkeleton(analyzed.ClassName, analyzed.TestMethods)
                    : fileContent;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "GenerateTestSkeleton failed; using local generator.");
                return TddHelpers.BuildCSharpTestSkeleton(analyzed.ClassName, analyzed.TestMethods);
            }
        }
    }
}

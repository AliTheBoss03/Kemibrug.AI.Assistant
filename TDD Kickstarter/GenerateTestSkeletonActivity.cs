using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Kemibrug.AI.Assistant.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant
{
    public class GenerateTestSkeletonActivity
    {
        private readonly ILogger<GenerateTestSkeletonActivity> _logger;
        private readonly OpenAIClient _openAIClient;
        private readonly ITestSkeletonBuilder _skeletonBuilder;

        public GenerateTestSkeletonActivity(ILogger<GenerateTestSkeletonActivity> logger, OpenAIClient openAIClient, ITestSkeletonBuilder skeletonBuilder)
        {
            _logger = logger;
            _openAIClient = openAIClient;
            _skeletonBuilder = skeletonBuilder;
        }

        [Function(nameof(GenerateTestSkeletonActivity))]
        public async Task<string> Run([ActivityTrigger] AnalyzedUserStory analyzedStory)
        {
            if (analyzedStory is null || string.IsNullOrWhiteSpace(analyzedStory.ClassName))
            {
                _logger.LogWarning("AnalyzedUserStory input was null or invalid. Returning a default skeleton.");
                return _skeletonBuilder.Build("GeneratedTests", Array.Empty<string>());
            }

            if (_openAIClient == null)
            {
                _logger.LogError("OpenAIClient is not configured. Falling back to local skeleton generator.");

                return _skeletonBuilder.Build(analyzedStory.ClassName, analyzedStory.TestMethods);
            }

            var deploymentName = "gpt-4o";
            if (string.IsNullOrEmpty(deploymentName))
            {
                throw new InvalidOperationException("Configuration error: AzureOpenAIDeploymentName is not set.");
            }

            try
            {
                var systemPrompt =
                    @"Role: Senior C# engineer specializing in test automation.
                    Task: Generate a complete C# xUnit test class file based on the provided class name and method names.
                    The response MUST contain ONLY the C# code for the test class. Do not include any other text, explanations, or markdown formatting.";

                var userPayload = $"Class Name: {analyzedStory.ClassName}, Test Methods: [{string.Join(", ", analyzedStory.TestMethods)}]";

                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    Temperature = 0.1f,
                    MaxTokens = 1500,
                    DeploymentName = deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage(systemPrompt),
                        new ChatRequestUserMessage(userPayload)
                    }
                };

                Response<ChatCompletions> response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                string generatedCode = response.Value.Choices[0].Message.Content;

                if (string.IsNullOrWhiteSpace(generatedCode) || !generatedCode.Contains($"class {analyzedStory.ClassName}"))
                {
                    _logger.LogWarning("AI did not return valid C# code. Falling back to local skeleton generator.");
                    return _skeletonBuilder.Build(analyzedStory.ClassName, analyzedStory.TestMethods);
                }

                _logger.LogInformation("Successfully generated test skeleton for class {className}", analyzedStory.ClassName);
                return generatedCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during test skeleton generation for class {className}.", analyzedStory.ClassName);
                throw;
            }
        }
    }
}
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;

namespace Kemibrug.AI.Assistant
{
    public static class AnalyzeCodeActivity
    {
        [Function("AnalyzeCodeActivity")]
        public static async Task<string> Run([ActivityTrigger] string codeToAnalyze, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("AnalyzeCodeActivity");

            var openAiEndpoint = Environment.GetEnvironmentVariable("AzureOpenAIEndpoint");
            var openAiKey = Environment.GetEnvironmentVariable("AzureOpenAIApiKey");
            var deploymentName = "gpt-4o";

            if (string.IsNullOrEmpty(openAiEndpoint) || string.IsNullOrEmpty(openAiKey))
            {
                logger.LogError("Azure OpenAI endpoint or key is not configured.");
                return "{\"error\": \"OpenAI configuration missing.\"}";
            }

            try
            {
                OpenAIClient client = new(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));

                var chatCompletionsOptions = new ChatCompletionsOptions()
                {
                    DeploymentName = deploymentName,
                    Messages =
                       {  
                           new ChatRequestUserMessage(codeToAnalyze)
                       },
                    MaxTokens = 800,
                    Temperature = 0.1f,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
                };

                logger.LogInformation("Sending code to Azure OpenAI for analysis using SDK...");

                Response<ChatCompletions> response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
                ChatResponseMessage responseMessage = response.Value.Choices[0].Message;

                logger.LogInformation("Analysis received from OpenAI.");

                return responseMessage.Content;
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while calling Azure OpenAI with SDK.");
                return $"{{\"error\": \"{e.Message}\"}}";
            }
        }
    }
}
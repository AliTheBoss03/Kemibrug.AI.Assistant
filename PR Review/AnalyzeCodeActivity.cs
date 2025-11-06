using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Kemibrug.AI.Assistant.Models.PR_Review;

namespace Kemibrug.AI.Assistant.PR_Review
{

    public class AnalyzeCodeActivity
    {
        private readonly OpenAIClient _openAIClient;
        private readonly IContextRetrievalService _contextRetrieval;
        private readonly ILogger<AnalyzeCodeActivity> _logger;

        public AnalyzeCodeActivity(
            OpenAIClient openAIClient,
            ILayerInferenceService layerInference,
            IContextRetrievalService contextRetrieval,
            ILogger<AnalyzeCodeActivity> logger)
        {
            _openAIClient = openAIClient;
            _contextRetrieval = contextRetrieval;
            _logger = logger;
        }

        [Function(nameof(AnalyzeCodeActivity))]
        public async Task<string> Run([ActivityTrigger] AnalysisInput input)
        {
            var deploymentName = "gpt-4o";
            var systemPrompt = GetRequiredEnvironmentVariable("AnalysisSystemPromptV2");

            try
            {
                _logger.LogInformation("Analyzing code for layer: {layer}", input.InferredLayer);

                var readmeContext = await _contextRetrieval.GetContextForLayerAsync(input.InferredLayer);

                var options = BuildChatOptions(deploymentName, systemPrompt, input.InferredLayer, readmeContext, input.CodeToAnalyze);

                Response<ChatCompletions> resp = await _openAIClient.GetChatCompletionsAsync(options);
                string content = resp.Value.Choices[0].Message.Content ?? string.Empty;

                ValidateResponseJson(content);

                _logger.LogInformation("Successfully received and validated analysis from OpenAI.");
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during code analysis.");
                throw;
            }
        }


        private ChatCompletionsOptions BuildChatOptions(string deploymentName, string systemPrompt, string layer, string context, string code)
        {
            var options = new ChatCompletionsOptions
            {
                Temperature = 0.1f,
                MaxTokens = 800,
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
                DeploymentName = deploymentName
            };

            options.Messages.Add(new ChatRequestSystemMessage(systemPrompt));

            if (!string.IsNullOrWhiteSpace(context))
            {
                options.Messages.Add(new ChatRequestSystemMessage(
                    $"Kemibrug project context (layer = {layer}):\n{context}"
                ));
            }

            options.Messages.Add(new ChatRequestUserMessage(
                @"CORRECT (Application -> interface via DI):
                   public class OrderService { private readonly IOrderRepository _repo; public OrderService(IOrderRepository repo){_repo=repo;} }"
            ));
            options.Messages.Add(new ChatRequestAssistantMessage(
                @"{""violationFound"":false,""violations"":[],""explanation"":""Service depends on interface via DI."",""meta"":{""layer"":""Application"",""promptVersion"":""2""}}"
            ));

            options.Messages.Add(new ChatRequestUserMessage(
                @"WRONG (Application -> concrete repo):
                public class WrongService { public Product Get(int id){ var repo = new ProductRepository(); return repo.GetById(id);} }"
            ));
            options.Messages.Add(new ChatRequestAssistantMessage(
                @"{""violationFound"":true,""violations"":[{""rule"":""Service uses concrete repository"",""principle"":""Onion/DI"",""severity"":""high"",""lines"":[1,2,3],""evidence"":""new ProductRepository() inside Application service"",""suggestion"":""Inject IProductRepository via constructor and depend on Core interface only""}],""explanation"":""Direct repo instantiation in Application."",""meta"":{""layer"":""Application"",""promptVersion"":""2""}}"
            ));

            options.Messages.Add(new ChatRequestUserMessage(
                $"Layer={layer}. Analyze ONLY the following C# code for rules R1–R5. Output JSON only.\n\n```csharp\n{code}\n```"
            ));

            return options;
        }

        private void ValidateResponseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new JsonException("Model response was null or empty.");
            }
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("violationFound", out _) ||
                    !doc.RootElement.TryGetProperty("explanation", out _))
                {
                    throw new JsonException($"Model response is valid JSON but is missing required fields ('violationFound', 'explanation'). Content: {json}");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Model response was not valid JSON or was missing fields.");
                throw;
            }
        }

        private static string GetRequiredEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"Configuration error: Environment variable '{name}' is not set.");
            }
            return value;
        }
    }
}
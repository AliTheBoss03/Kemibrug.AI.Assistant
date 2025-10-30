namespace Kemibrug.AI.Assistant
{
    using Azure;
    using Azure.AI.OpenAI;
    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="AnalyzeCodeActivity" />
    /// </summary>
    public static class AnalyzeCodeActivity
    {
        /// <summary>
        /// The Run
        /// </summary>
        /// <param name="codeToAnalyze">The codeToAnalyze<see cref="string"/></param>
        /// <param name="ctx">The ctx<see cref="FunctionContext"/></param>
        /// <returns>The <see cref="Task{string}"/></returns>
        [Function("AnalyzeCodeActivity")]
        public static async Task<string> Run([ActivityTrigger] string codeToAnalyze, FunctionContext ctx)
        {
            var log = ctx.GetLogger("AnalyzeCodeActivity");

            var endpoint = Environment.GetEnvironmentVariable("AzureOpenAIEndpoint");
            var apiKey = Environment.GetEnvironmentVariable("AzureOpenAIApiKey");
            var deploymentName = "gpt-4o";

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
            {
                log.LogError("Azure OpenAI endpoint or key is not configured.");
                return "{\"error\":\"OpenAI configuration missing.\"}";
            }

            try
            {
                var client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

                var options = new ChatCompletionsOptions
                {
                    Temperature = 0.1f,
                    MaxTokens = 800,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
                    DeploymentName = deploymentName
                };

                options.Messages.Add(new ChatRequestSystemMessage(
                    "Role: Senior .NET software architect specialized in Onion Architecture. " +
                    "Task: Review code strictly for ONE rule: Service layer MUST NOT directly instantiate or call repository classes. " +
                    "All data access must go through an application/service interface (e.g., IProductRepository). " +
                    "If the rule is violated (e.g., new ProductRepository() inside a service), flag it. " +
                    "Respond ONLY with a valid JSON object (no prose) matching: " +
                    "{ \"violationFound\": boolean, \"explanation\": string }"
                ));

                options.Messages.Add(new ChatRequestUserMessage(
                    @"KORREKT:
                    public class CorrectOrderService {
                        private readonly IOrderRepository _orderRepository;
                    }"
                ));
                options.Messages.Add(new ChatRequestAssistantMessage(
                    @"{""violationFound"": false, ""explanation"": ""Uses interface IOrderRepository; no direct repo instantiation.""}"
                ));

                options.Messages.Add(new ChatRequestUserMessage(
                    @"FORKERT:
                    public class WrongProductService {
                        public Product GetProduct(int id) {
                            var repo = new ProductRepository(); // direkte repo i service
                            return repo.GetById(id);
                        }
                    }"));
                options.Messages.Add(new ChatRequestAssistantMessage(
                    @"{""violationFound"": true, ""explanation"": ""Service layer instantiates ProductRepository directly instead of using interface.""}"
                ));

                // Selve koden fra PR
                options.Messages.Add(new ChatRequestUserMessage(
                    "Analyze ONLY the following C# code for the specified rule. Output JSON only.\n\n```csharp\n" +
                    codeToAnalyze + "\n```"
                ));

                var resp = await client.GetChatCompletionsAsync(options);
                var content = resp.Value.Choices[0].Message.Content ?? string.Empty;

                // Let JSON-validering (fail-fast hvis modellen ikke holdt kontrakten)
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                        !doc.RootElement.TryGetProperty("violationFound", out _) ||
                        !doc.RootElement.TryGetProperty("explanation", out _))
                    {
                        log.LogWarning("Model response missing required fields. Content: {Content}", content);
                        return "{\"error\":\"Model response missing required fields.\"}";
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Model response was not valid JSON. Content: {Content}", content);
                    return "{\"error\":\"Model response was not valid JSON.\"}";
                }

                log.LogInformation("Analysis received from OpenAI.");
                return content;
            }
            catch (RequestFailedException rex)
            {
                // Azure OpenAI-specifik fejl
                log.LogError(rex, "Azure OpenAI request failed. Status: {Status}", rex.Status);
                return $"{{\"error\":\"Azure OpenAI request failed (status {rex.Status}).\"}}";
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unexpected error while calling Azure OpenAI.");
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }
    }
}

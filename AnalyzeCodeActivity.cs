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
                    "role_v2: Senior .NET architect specialized in Onion Architecture for Kemibrug. " +
                    "You are executing step 2/3 (static analysis) of a prompt chain. " +
                    "Check ONLY these rules and return JSON:\n" +
                    "R1 Onion/Layers: (a) Application/Service must NOT instantiate concrete repositories or DbContext; depend only on Core interfaces via DI. " +
                    "(b) Core MUST NOT reference Application/Infrastructure/WebApiServer. " +
                    "(c) WebApiServer MUST call Application via interfaces and MUST return DTOs (no domain entities). " +
                    "(d) Infrastructure MUST NOT contain business logic (persistence/mapping only).\n" +
                    "R2 DTO rule: API returns DTOs to clients (no domain entities over the wire).\n" +
                    "R3 DI rule: Dependencies injected (no `new Sql...Repository()`, no `new DbContext()` inside services/controllers).\n" +
                    "R4 DRY: Flag obvious duplication in THIS file and suggest extraction.\n" +
                    "R5 SRP: Flag classes/methods doing multiple responsibilities or excessively long methods.\n" +
                    "Respond ONLY with a valid JSON object (no prose) matching exactly:\n" +
                    "{ \"violationFound\": boolean,\n" +
                    "  \"violations\": [\n" +
                    "    { \"rule\": string, \"principle\": string, \"severity\": \"low\"|\"medium\"|\"high\",\n" +
                    "      \"lines\": [number], \"evidence\": string, \"suggestion\": string }\n" +
                    "  ],\n" +
                    "  \"explanation\": string,\n" +
                    "  \"meta\": { \"layer\": string, \"promptVersion\": \"2\" }\n" +
                    "}"
                ));

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

                // Selve koden fra PR
                options.Messages.Add(new ChatRequestUserMessage(
                    "Layer=Application. Analyze ONLY the following C# code for rules R1–R5. Output JSON only.\n\n```csharp\n" +
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

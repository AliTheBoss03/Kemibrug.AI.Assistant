using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant
{
    public static class AnalyzeCodeActivity
    {
        // Limits to keep prompts compact and safe
        private const int MaxReadmeDocs = 4;
        private const int MaxCharsPerDoc = 6000;
        private const int MaxTotalContextChars = 14000;

        [Function("AnalyzeCodeActivity")]
        public static async Task<string> Run([ActivityTrigger] string codeToAnalyze, FunctionContext ctx)
        {
            var log = ctx.GetLogger("AnalyzeCodeActivity");

            // === OpenAI config ===
            var endpoint = Environment.GetEnvironmentVariable("AzureOpenAIEndpoint");
            var apiKey = Environment.GetEnvironmentVariable("AzureOpenAIApiKey");
            var deploymentName = "gpt-4o";

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
            {
                log.LogError("Azure OpenAI endpoint or key is not configured.");
                return "{\"error\":\"OpenAI configuration missing.\"}";
            }

            // === Retrieval config (Azure AI Search) ===
            var searchEndpoint = Environment.GetEnvironmentVariable("AzureSearchEndpoint");
            var searchIndex = Environment.GetEnvironmentVariable("AzureSearchIndex");
            var searchApiKey = Environment.GetEnvironmentVariable("AzureSearchApiKey");

            // === System prompt (moved into env) ===
            var systemPrompt = Environment.GetEnvironmentVariable("AnalysisSystemPromptV2");

            try
            {
                // 1) Infer layer from the code blob (based on file headers assembled by GetPullRequestChangesActivity)
                var inferredLayer = InferLayerFromCode(codeToAnalyze);
                log.LogInformation("Inferred layer for analysis: {layer}", inferredLayer);

                // 2) Retrieve README context for the inferred layer (best-effort)
                string readmeContext = string.Empty;
                if (!string.IsNullOrWhiteSpace(searchEndpoint) &&
                    !string.IsNullOrWhiteSpace(searchIndex) &&
                    !string.IsNullOrWhiteSpace(searchApiKey))
                {
                    try
                    {
                        readmeContext = await FetchReadmeContextAsync(
                            searchEndpoint, searchIndex, searchApiKey, inferredLayer, log);
                    }
                    catch (Exception rex)
                    {
                        log.LogWarning(rex, "Fetching README context from Azure AI Search failed; continuing without retrieval context.");
                    }
                }
                else
                {
                    log.LogInformation("Azure AI Search not configured (Endpoint/Index/ApiKey missing) — proceeding without README context.");
                }

                // 3) Build the chat with system + (optional) context + examples + code
                var client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                var options = new ChatCompletionsOptions
                {
                    Temperature = 0.1f,
                    MaxTokens = 800,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
                    DeploymentName = deploymentName
                };

                // System role
                options.Messages.Add(new ChatRequestSystemMessage(systemPrompt));

                // Optional contextual system message with retrieved README(s)
                if (!string.IsNullOrWhiteSpace(readmeContext))
                {
                    // Keep it as system so it’s treated as higher-priority rules/reference
                    options.Messages.Add(new ChatRequestSystemMessage(
                        $"Kemibrug project context (layer = {inferredLayer}):\n{readmeContext}"
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

                // Actual code payload
                options.Messages.Add(new ChatRequestUserMessage(
                    $"Layer={inferredLayer}. Analyze ONLY the following C# code for rules R1–R5. Output JSON only.\n\n```csharp\n{codeToAnalyze}\n```"
                ));

                var resp = await client.GetChatCompletionsAsync(options);
                var content = resp.Value.Choices[0].Message.Content ?? string.Empty;

                // Validate returned JSON contract
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
                // Azure OpenAI-specific failure
                ctx.GetLogger("AnalyzeCodeActivity").LogError(rex, "Azure OpenAI request failed. Status: {Status}", rex.Status);
                return $"{{\"error\":\"Azure OpenAI request failed (status {rex.Status}).\"}}";
            }
            catch (Exception ex)
            {
                ctx.GetLogger("AnalyzeCodeActivity").LogError(ex, "Unexpected error while calling Azure OpenAI.");
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// Very simple layer inference from file headers added by GetPullRequestChangesActivity.
        /// Falls back to Application if not determinable.
        /// </summary>
        private static string InferLayerFromCode(string combinedCode)
        {
            if (string.IsNullOrWhiteSpace(combinedCode))
                return "Application";

            if (combinedCode.IndexOf("/KemibrugV2.WebApiServer/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "WebApiServer";
            if (combinedCode.IndexOf("/KemibrugV2.Application/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Application";
            if (combinedCode.IndexOf("/KemibrugV2.Infrastructure/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Infrastructure";
            if (combinedCode.IndexOf("/KemibrugV2.Core/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Core";

            if (combinedCode.IndexOf("/Application/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Application";
            if (combinedCode.IndexOf("/WebApiServer/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "WebApiServer";
            if (combinedCode.IndexOf("/Infrastructure/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Infrastructure";
            if (combinedCode.IndexOf("/Core/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Core";

            return "Application";
        }

        /// <summary>
        /// Queries Azure AI Search for README docs matching the layer and builds a compact context string.
        /// </summary>
        private static async Task<string> FetchReadmeContextAsync(
            string searchEndpoint,
            string searchIndex,
            string apiKey,
            string layer,
            ILogger log)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("api-key", apiKey);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var searchUrl = $"{searchEndpoint.TrimEnd('/')}/indexes/{Uri.EscapeDataString(searchIndex)}/docs/search?api-version=2023-11-01";

            // Filter strictly by layer field (as we indexed it)
            var payload = new
            {
                search = "*",
                top = MaxReadmeDocs,
                filter = $"layer eq '{layer}'",
                select = "id,path,layer,content"
            };

            var reqBody = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var resp = await http.PostAsync(searchUrl, reqBody);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("Azure AI Search request failed: {Status} {Body}", (int)resp.StatusCode, body);
                return string.Empty;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("value", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
            {
                log.LogInformation("Azure AI Search returned no README docs for layer {layer}.", layer);
                return string.Empty;
            }

            var sb = new StringBuilder();
            int remaining = MaxTotalContextChars;

            foreach (var item in items.EnumerateArray())
            {
                var path = item.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "(unknown path)";
                var text = item.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var cleaned = StripLeadingGetLine(text);

                if (cleaned.Length > MaxCharsPerDoc) cleaned = cleaned.Substring(0, MaxCharsPerDoc);

                var header = $"\n### README: {path}\n";
                var needed = header.Length + cleaned.Length;
                if (needed > remaining) break;

                sb.Append(header);
                sb.Append(cleaned);
                remaining -= needed;
            }

            return sb.ToString();
        }

        private static string StripLeadingGetLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var idx = text.IndexOf('\n');
            if (idx > 0)
            {
                var firstLine = text.Substring(0, idx);
                if (firstLine.StartsWith("GET ", StringComparison.OrdinalIgnoreCase))
                    return text.Substring(idx + 1);
            }
            return text;
        }
    }
}

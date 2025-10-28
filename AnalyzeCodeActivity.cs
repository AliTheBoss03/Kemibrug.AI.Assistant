using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kemibrug.AI.Assistant
{
    public static class AnalyzeCodeActivity
    {
        private static readonly HttpClient _httpClient = new HttpClient();

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

            string systemPrompt = @"
                **Rolle:** Som en erfaren softwarearkitekt med specialisering i .NET og Onion Archi-
                tecture, som den praktiseres i Kemibrug-projektet, er din opgave at reviewe kode
                for brud p˚a arkitekturregler. Du er ekstremt præcis og detaljeorienteret.
                
                **Kontekst:** Jeg har et system, der følger Onion Architecture. En fundamental og
                ufravigelig regel er: Kode i Service-laget m˚a ALDRIG kalde direkte p˚a en
                klasse i Repository-laget. Al datatilgang skal g˚a gennem et Application Service-
                interface (f.eks. IProductRepository). Direkte instansiering med new ProductRepository()
                i en service er et alvorligt brud p˚a reglen.
                

                **Eksempler:** Kode som overholder reglen: 
                public class CorrectOrderService
                {
                    private readonly IOrderRepository _orderRepository;
                    // ... (resten af koden)
                }
                
                Kode som ikke overholder reglen:
                
                public class WrongProductService
                {
                    public Product GetProduct(int id)
                    {
                        var repo = new ProductRepository(); // FORKERT
                        return repo.GetById(id);
                    }
                }
                **Opgave:**
                Analyser nu KUN det følgende C#-kodestykke for brud på den specifikke regel beskrevet ovenfor.

                **Struktureret Output:** Du SKAL formatere dit svar som et JSON-objekt med
                felterne ""violationFound"" (boolean) og ""explanation"" (string). Hvis reglen er
                overholdt, skal ""violationFound"" være false.
            ";

            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = codeToAnalyze }
                },
                max_tokens = 800,
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{openAiEndpoint}/openai/deployments/{deploymentName}/chat/completions?api-version=2024-02-15-preview");
            request.Headers.Add("api-key", openAiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                logger.LogInformation("Sending code to Azure OpenAI for analysis...");
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                var openAiResponse = JsonDocument.Parse(responseBody);
                var messageContent = openAiResponse.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                logger.LogInformation("Analysis received from OpenAI.");
                return messageContent;
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while calling Azure OpenAI.");
                return $"{{\"error\": \"{e.Message}\"}}";
            }
        }
    }
}
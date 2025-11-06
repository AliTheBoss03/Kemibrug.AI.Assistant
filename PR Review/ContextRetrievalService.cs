using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kemibrug.AI.Assistant.PR_Review
{
    public interface IContextRetrievalService
    {
        Task<string> GetContextForLayerAsync(string layer);
    }

    public class ContextRetrievalService : IContextRetrievalService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ContextRetrievalService> _logger;
        private const int MaxReadmeDocs = 4;
        private const int MaxCharsPerDoc = 6000;
        private const int MaxTotalContextChars = 14000;

        public ContextRetrievalService(IHttpClientFactory httpClientFactory, ILogger<ContextRetrievalService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> GetContextForLayerAsync(string layer)
        {
            var searchEndpoint = Environment.GetEnvironmentVariable("AzureSearchEndpoint");
            var searchIndex = Environment.GetEnvironmentVariable("AzureSearchIndex");
            var searchApiKey = Environment.GetEnvironmentVariable("AzureSearchApiKey");

            if (string.IsNullOrWhiteSpace(searchEndpoint) || string.IsNullOrWhiteSpace(searchIndex) || string.IsNullOrWhiteSpace(searchApiKey))
            {
                _logger.LogInformation("Azure AI Search is not configured; skipping context retrieval.");
                return string.Empty;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("api-key", searchApiKey);

                var searchUrl = $"{searchEndpoint.TrimEnd('/')}/indexes/{Uri.EscapeDataString(searchIndex)}/docs/search?api-version=2023-11-01";
                var payload = new { search = "*", top = MaxReadmeDocs, filter = $"layer eq '{layer}'", select = "id,path,layer,content" };
                var requestBody = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(searchUrl, requestBody);
                response.EnsureSuccessStatusCode();

                return await ParseSearchResponse(await response.Content.ReadAsStringAsync(), layer);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fetching README context from Azure AI Search failed. Continuing without retrieval context.");
                return string.Empty;
            }
        }

        private async Task<string> ParseSearchResponse(string body, string layer)
        {

            _logger.LogInformation("Successfully retrieved context for layer {layer}.", layer);
            return "Parsed context from AI Search...";
        }
    }
}
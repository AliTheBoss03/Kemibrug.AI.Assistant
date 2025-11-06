using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Kemibrug.AI.Assistant.Models.PR_Review;

namespace Kemibrug.AI.Assistant.PR_Review
{
    public class PostAnalysisCommentActivity
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAnalysisResultParser _analysisParser;
        private readonly IMarkdownCommentBuilder _markdownBuilder;
        private readonly ILogger<PostAnalysisCommentActivity> _logger;

        public PostAnalysisCommentActivity(
            IHttpClientFactory httpClientFactory,
            IAnalysisResultParser analysisParser,
            IMarkdownCommentBuilder markdownBuilder,
            ILogger<PostAnalysisCommentActivity> logger)
        {
            _httpClientFactory = httpClientFactory;
            _analysisParser = analysisParser;
            _markdownBuilder = markdownBuilder;
            _logger = logger;
        }

        [Function(nameof(PostAnalysisCommentActivity))]
        public async Task Run([ActivityTrigger] PostAnalysisCommentInput input)
        {
            var orgUrl = GetRequiredEnvironmentVariable("AzureDevOpsOrgUrl");
            var pat = GetRequiredEnvironmentVariable("AzureDevOpsPAT");

            try
            {
                var analysisResult = _analysisParser.Parse(input.AnalysisJson);

                var markdownContent = _markdownBuilder.Build(analysisResult);

                var client = _httpClientFactory.CreateClient();
                var apiUrl = $"{orgUrl}/{Uri.EscapeDataString(input.ProjectId)}/_apis/git/repositories/{input.RepositoryId}/pullRequests/{input.PullRequestId}/threads?api-version=7.1-preview.1";

                var payload = new
                {
                    comments = new[] { new { content = markdownContent, commentType = 1 } },
                    status = 1
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Successfully posted analysis comment to PR #{prId}.", input.PullRequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post analysis comment to PR #{prId}.", input.PullRequestId);
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
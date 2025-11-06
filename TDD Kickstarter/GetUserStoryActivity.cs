using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kemibrug.AI.Assistant.Models;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Kemibrug.AI.Assistant
{
    public class GetUserStoryActivity
    {
        private readonly ILogger<GetUserStoryActivity> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // Use constructor injection for dependencies
        public GetUserStoryActivity(ILogger<GetUserStoryActivity> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [Function(nameof(GetUserStoryActivity))]
        public async Task<UserStoryInput> Run([ActivityTrigger] int userStoryId)
        {
            _logger.LogInformation("Fetching data for User Story ID: {userStoryId}", userStoryId);

            // Fetch configuration
            var pat = GetRequiredEnvironmentVariable("AzureDevOpsPAT");
            var orgUrl = GetRequiredEnvironmentVariable("AzureDevOpsOrgUrl");
            var projectName = GetRequiredEnvironmentVariable("AzureDevOpsProjectName");

            // Create a client from the factory
            var client = _httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{orgUrl}/{Uri.EscapeDataString(projectName)}/_apis/wit/workitems/{userStoryId}?api-version=7.1");

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            try
            {
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<WorkItemApiResponse>(responseBody);

                var userStoryInput = new UserStoryInput
                {
                    Title = apiResponse.Fields.Title,
                    Description = CleanHtml(apiResponse.Fields.Description),
                    AcceptanceCriteria = ParseAcceptanceCriteria(apiResponse.Fields.AcceptanceCriteriaHtml)
                };

                _logger.LogInformation("Successfully fetched and parsed User Story '{title}'", userStoryInput.Title);
                return userStoryInput;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch User Story with ID {userStoryId}", userStoryId);
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

        private static string CleanHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return Regex.Replace(html, "<.*?>", string.Empty);
        }

        private static List<string> ParseAcceptanceCriteria(string html)
        {
            if (string.IsNullOrEmpty(html)) return new List<string>();
            var matches = Regex.Matches(html, @"<li>(.*?)</li>", RegexOptions.Singleline);
            return matches.Cast<Match>().Select(m => CleanHtml(m.Groups[1].Value).Trim()).ToList();
        }
    }
}
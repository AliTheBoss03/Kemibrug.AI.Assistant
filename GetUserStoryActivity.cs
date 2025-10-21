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

namespace Kemibrug.AI.Assistant
{
    public static class GetUserStoryActivity
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        [Function("GetUserStoryActivity")]
        public static async Task<UserStoryInput> Run([ActivityTrigger] int userStoryId, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("GetUserStoryActivity");
            logger.LogInformation("Fetching data for User Story ID: {id}", userStoryId);

            var pat = Environment.GetEnvironmentVariable("AzureDevOpsPAT");
            var orgUrl = Environment.GetEnvironmentVariable("AzureDevOpsOrgUrl");
            var projectName = "AI-assisted%20Quality%20Assurance%20and%20Test%20Automation";

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            var requestUrl = $"{orgUrl}/{projectName}/_apis/wit/workitems/{userStoryId}?api-version=7.1";

            try
            {
                var response = await _httpClient.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<WorkItemApiResponse>(responseBody);

                var userStoryInput = new UserStoryInput
                {
                    Title = apiResponse.Fields.Title,
                    Description = CleanHtml(apiResponse.Fields.Description),
                    AcceptanceCriteria = ParseAcceptanceCriteria(apiResponse.Fields.AcceptanceCriteriaHtml)
                };

                logger.LogInformation("Successfully fetched and parsed User Story '{title}'", userStoryInput.Title);
                return userStoryInput;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error fetching User Story with ID {id}", userStoryId);
                return new UserStoryInput { Title = "Error fetching story" };
            }
        }

        private static string CleanHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return Regex.Replace(html, "<.*?>", string.Empty);
        }

        private static List<string> ParseAcceptanceCriteria(string html)
        {
            if (string.IsNullOrEmpty(html)) return new List<string>();
            var matches = Regex.Matches(html, @"<li>(.*?)</li>");
            return matches.Cast<Match>().Select(m => CleanHtml(m.Groups[1].Value)).ToList();
        }
    }
}
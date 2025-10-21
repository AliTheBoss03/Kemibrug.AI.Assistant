using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kemibrug.AI.Assistant.Models;

namespace Kemibrug.AI.Assistant
{
    public static class GetPullRequestChangesActivity
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        [Function("GetPullRequestChangesActivity")]
        public static async Task<string> Run([ActivityTrigger] Resource pullRequest, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("GetPullRequestChangesActivity");
            logger.LogInformation("Fetching changes for PR {prId}", pullRequest.PullRequestId);

            var pat = Environment.GetEnvironmentVariable("AzureDevOpsPAT");
            var orgUrl = Environment.GetEnvironmentVariable("AzureDevOpsOrgUrl");

            if (string.IsNullOrEmpty(pat) || string.IsNullOrEmpty(orgUrl))
            {
                logger.LogError("Azure DevOps configuration is missing.");
                return "Error: Configuration missing.";
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            try
            {
                var commitsUrl = $"{orgUrl}/_apis/git/repositories/{pullRequest.Repository.Id}/pullrequests/{pullRequest.PullRequestId}/commits?api-version=7.1-preview.1";
                var commitsResponse = await _httpClient.GetAsync(commitsUrl);
                commitsResponse.EnsureSuccessStatusCode();
                var commitsBody = await commitsResponse.Content.ReadAsStringAsync();
                var commitList = JsonSerializer.Deserialize<CommitListResponse>(commitsBody);
                var latestCommitId = commitList?.Value?.FirstOrDefault()?.CommitId;

                if (string.IsNullOrEmpty(latestCommitId))
                {
                    logger.LogWarning("No commits found in PR.");
                    return string.Empty;
                }

                var changesUrl = $"{orgUrl}/_apis/git/repositories/{pullRequest.Repository.Id}/commits/{latestCommitId}/changes?api-version=7.1-preview.1";
                var changesResponse = await _httpClient.GetAsync(changesUrl);
                changesResponse.EnsureSuccessStatusCode();
                var changesBody = await changesResponse.Content.ReadAsStringAsync();
                var changesData = JsonSerializer.Deserialize<CommitChangesResponse>(changesBody);

                var codeBuilder = new StringBuilder();

                foreach (var change in changesData.Changes.Where(c => c.Item.Path.EndsWith(".cs") && c.ChangeType != "delete"))
                {
                    logger.LogInformation("Fetching content for file: {filePath}", change.Item.Path);

                    var fileContentUrl = change.Item.Url;
                    var fileContent = await _httpClient.GetStringAsync(fileContentUrl);

                    codeBuilder.AppendLine($"--- FILE: {change.Item.Path} ---");
                    codeBuilder.AppendLine(fileContent);
                    codeBuilder.AppendLine("--- END OF FILE ---");
                }

                string finalCode = codeBuilder.ToString();
                logger.LogInformation("Successfully fetched and combined content of {count} C# file(s).", changesData.Changes.Count(c => c.Item.Path.EndsWith(".cs")));

                return finalCode.Length > 0 ? finalCode : "No C# files were changed in this PR.";
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while fetching PR changes.");
                return $"Error: {e.Message}";
            }
        }
    }
}
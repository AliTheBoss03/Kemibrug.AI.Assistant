using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kemibrug.AI.Assistant.Models.AzureDevOps;
using Kemibrug.AI.Assistant.Models.PR_Review;

namespace Kemibrug.AI.Assistant
{
    public class GetPullRequestChangesActivity
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GetPullRequestChangesActivity> _logger;

        public GetPullRequestChangesActivity(IHttpClientFactory httpClientFactory, ILogger<GetPullRequestChangesActivity> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [Function(nameof(GetPullRequestChangesActivity))]
        public async Task<string> Run([ActivityTrigger] PullRequestOrchestratorInput input)
        {
            _logger.LogInformation("Fetching changes for PR {prId}", input.PullRequestId);

            var pat = GetRequiredEnvironmentVariable("AzureDevOpsPAT");
            var orgUrl = GetRequiredEnvironmentVariable("AzureDevOpsOrgUrl");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            try
            {
                var commitsUrl = $"{orgUrl}/_apis/git/repositories/{input.RepositoryId}/pullrequests/{input.PullRequestId}/commits?api-version=7.1-preview.1";
                var commitsResponse = await client.GetAsync(commitsUrl);
                commitsResponse.EnsureSuccessStatusCode();

                var commitList = await JsonSerializer.DeserializeAsync<CommitListResponse>(await commitsResponse.Content.ReadAsStreamAsync());
                var latestCommitId = commitList?.Value?.FirstOrDefault()?.CommitId;

                if (string.IsNullOrEmpty(latestCommitId))
                {
                    _logger.LogWarning("No commits found in PR {prId}.", input.PullRequestId);
                    return string.Empty;
                }

                var changesUrl = $"{orgUrl}/_apis/git/repositories/{input.RepositoryId}/commits/{latestCommitId}/changes?api-version=7.1-preview.1";
                var changesResponse = await client.GetAsync(changesUrl);
                changesResponse.EnsureSuccessStatusCode();
                var changesData = await JsonSerializer.DeserializeAsync<CommitChangesResponse>(await changesResponse.Content.ReadAsStreamAsync());

                var codeBuilder = new StringBuilder();
                var csChanges = changesData?.Changes.Where(c => c.Item.Path.EndsWith(".cs") && c.ChangeType != "delete").ToList() ?? new();

                foreach (var change in csChanges)
                {
                    _logger.LogInformation("Fetching content for file: {filePath}", change.Item.Path);
                    var fileContent = await client.GetStringAsync(change.Item.Url);

                    codeBuilder.AppendLine($"--- FILE: {change.Item.Path} ---");
                    codeBuilder.AppendLine(fileContent);
                    codeBuilder.AppendLine("--- END OF FILE ---");
                }

                _logger.LogInformation("Successfully fetched and combined content of {count} C# file(s).", csChanges.Count);
                return codeBuilder.ToString();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while fetching PR changes for PR {prId}.", input.PullRequestId);
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
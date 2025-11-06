using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Kemibrug.AI.Assistant.Models;

namespace Kemibrug.AI.Assistant
{
    public class UserStoryTrigger
    {
        private readonly ILogger<UserStoryTrigger> _logger;

        public UserStoryTrigger(ILogger<UserStoryTrigger> logger)
        {
            _logger = logger;
        }

        [Function("UserStoryTrigger")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            int? extractedId = TryExtractWorkItemId(requestBody);

            if (extractedId is null or <= 0)
            {
                _logger.LogError("Could not extract a valid User Story ID from the request body. Body: {requestBody}", requestBody);
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var workItemId = extractedId.Value;
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("TddKickstarterOrchestrator", workItemId);
            _logger.LogInformation("Started TDD orchestration for User Story ID {id}. Instance ID: {instanceId}", workItemId, instanceId);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        private int? TryExtractWorkItemId(string requestBody)
        {
            if (string.IsNullOrEmpty(requestBody))
            {
                _logger.LogWarning("Request body was empty.");
                return null;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                var fullWebhook = JsonSerializer.Deserialize<AzureDevOpsWebhook>(requestBody, options);
                if (fullWebhook?.Resource?.Id > 0)
                {
                    _logger.LogInformation("Extracted work item ID from full webhook payload.");
                    return fullWebhook.Resource.Id;
                }
            }
            catch (JsonException)
            {
            }

            return null;
        }
    }
}
using Kemibrug.AI.Assistant.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

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
            int? workItemId = await TryExtractWorkItemId(req);

            if (workItemId is null or <= 0)
            {
                _logger.LogError("Could not extract a valid User Story ID from the request body.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("TddKickstarterOrchestrator", workItemId.Value);
            _logger.LogInformation("Started TDD orchestration for User Story ID {id}. Instance ID: {instanceId}", workItemId.Value, instanceId);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        private async Task<int?> TryExtractWorkItemId(HttpRequestData req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                _logger.LogWarning("Request body was empty.");
                return null;
            }

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var payload = JsonSerializer.Deserialize<WebhookPayload>(requestBody, options);

                // Logic to find the ID from the unified payload
                return payload?.Id ?? payload?.Resource?.WorkItemId ?? payload?.Resource?.ResourceId;
            }
            catch (JsonException ex)
            {
                // Log the actual exception
                _logger.LogError(ex, "Failed to deserialize request body. Body: {body}", requestBody);
                return null;
            }
        }
    }
}